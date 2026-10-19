"""Render reviewable M2 policy JSON locally. Standard library only; no AWS calls.

These are M2-authored templates derived from M3-01_ban2 section 7.8.
They are not the missing M3 ZIP, IAM simulation results or deployment approval.
"""
import argparse
from copy import deepcopy
import json
from pathlib import Path
import re

REGION = "ap-southeast-1"
SESSION_LOG = "/aieb/staging/ssm-sessions"
READ_PARAMETER = ["ssm:GetParameter", "ssm:GetParameters"]
OTHER_PARAMETER = ["ssm:GetParametersByPath", "ssm:GetParameterHistory", "ssm:PutParameter", "ssm:DeleteParameter", "ssm:DeleteParameters"]
# The agent permissions below cap the managed core policy in a boundary.
# Parameter reads are granted separately, never as an agent wildcard.
AGENT_ACTIONS = [
    "ssm:DescribeAssociation", "ssm:GetDeployablePatchSnapshotForInstance",
    "ssm:GetDocument", "ssm:DescribeDocument", "ssm:GetManifest",
    "ssm:ListAssociations", "ssm:ListInstanceAssociations", "ssm:PutInventory",
    "ssm:PutComplianceItems", "ssm:PutConfigurePackageResult",
    "ssm:UpdateAssociationStatus", "ssm:UpdateInstanceAssociationStatus",
    "ssm:UpdateInstanceInformation", "ssmmessages:CreateControlChannel",
    "ssmmessages:CreateDataChannel", "ssmmessages:OpenControlChannel",
    "ssmmessages:OpenDataChannel", "ec2messages:AcknowledgeMessage",
    "ec2messages:DeleteMessage", "ec2messages:FailMessage", "ec2messages:GetEndpoint",
    "ec2messages:GetMessages", "ec2messages:SendReply",
]


def policy(*statements):
    return {"Version": "2012-10-17", "Statement": list(statements)}


def statement(sid, effect, actions, resources=None, *, not_resources=None, condition=None, principal=None):
    result = {"Sid": sid, "Effect": effect, "Action": actions}
    if resources is not None:
        result["Resource"] = resources
    if not_resources is not None:
        result["NotResource"] = not_resources
    if condition:
        result["Condition"] = condition
    if principal:
        result["Principal"] = principal
    return result


def validate_inputs(config):
    required = {
        "account_id", "deployment_principal_arn", "key_admin_arn", "app_custodian_arn",
        "db_custodian_arn", "operator_session_prefix", "dba_session_prefix",
        "entry_instance_id", "app_instance_id", "db_instance_id",
        "app_kms_key_id", "db_kms_key_id",
    }
    if set(config) != required:
        raise ValueError("Input keys must exactly match policy-inputs.example.json")
    if not re.fullmatch(r"[0-9]{12}", config["account_id"]):
        raise ValueError("account_id must contain 12 digits")
    for key in ["deployment_principal_arn", "key_admin_arn", "app_custodian_arn", "db_custodian_arn"]:
        if not re.fullmatch(rf"arn:aws:iam::{config['account_id']}:role/[A-Za-z0-9+=,.@_/-]+", config[key]):
            raise ValueError(f"{key} must be one exact IAM role ARN in the selected account")
    for key in ["entry_instance_id", "app_instance_id", "db_instance_id"]:
        if not re.fullmatch(r"i-[0-9a-f]{17}", config[key]):
            raise ValueError(f"{key} must be an EC2 instance ID")
    for key in ["app_kms_key_id", "db_kms_key_id"]:
        if not re.fullmatch(r"[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}", config[key]):
            raise ValueError(f"{key} must be a symmetric key UUID")
    if config["app_kms_key_id"] == config["db_kms_key_id"]:
        raise ValueError("App and DB must use different customer-managed KMS keys")
    if config["app_custodian_arn"] == config["db_custodian_arn"]:
        raise ValueError("App and DB custodians must be separately assigned roles")
    principals = [config[key] for key in ["deployment_principal_arn", "key_admin_arn", "app_custodian_arn", "db_custodian_arn"]]
    reserved = {f"arn:aws:iam::{config['account_id']}:role/aieb-staging-{host}-role" for host in ["entry", "app", "db", "terraform"]}
    if len(set(principals)) != 4 or reserved.intersection(principals):
        raise ValueError("Deployment, key administration and custodian roles must be distinct and cannot be runtime/execution roles")
    if len({config["entry_instance_id"], config["app_instance_id"], config["db_instance_id"]}) != 3:
        raise ValueError("The three host IDs must be distinct")
    for key in ["operator_session_prefix", "dba_session_prefix"]:
        if not re.fullmatch(r"[A-Za-z0-9_+=,.@-]{1,128}", config[key]):
            raise ValueError("Use a reviewed session-owner prefix, not a wildcard or arbitrary IAM username")


def build_bundle(config):
    validate_inputs(config)
    account = config["account_id"]
    data_bucket = f"aieb-staging-data-{account}-{REGION}"
    state_bucket = f"aieb-tfstate-{account}-{REGION}"
    data_arn = f"arn:aws:s3:::{data_bucket}"
    state_arn = f"arn:aws:s3:::{state_bucket}"
    role_arns = {host: f"arn:aws:iam::{account}:role/aieb-staging-{host}-role" for host in ["entry", "app", "db"]}
    parameters = {"app": f"arn:aws:ssm:{REGION}:{account}:parameter/aieb/staging/app/connection-string",
                  "db": f"arn:aws:ssm:{REGION}:{account}:parameter/aieb/staging/db/bootstrap-password"}
    keys = {host: f"arn:aws:kms:{REGION}:{account}:key/{config[host + '_kms_key_id']}" for host in ["app", "db"]}
    bundles = {}
    bundles["ec2-trust.json"] = policy(statement("Ec2Trust", "Allow", "sts:AssumeRole", principal={"Service": "ec2.amazonaws.com"}))
    bundles["terraform-trust.json"] = policy(statement("TemporaryOperatorTrust", "Allow", "sts:AssumeRole", principal={"AWS": config["deployment_principal_arn"]}))

    for host in ["entry", "app", "db"]:
        logs = [f"arn:aws:logs:{REGION}:{account}:log-group:/aieb/staging/{group}:log-stream:*" for group in [host, "ssm-sessions"]]
        rules = [statement("OwnLogStreams", "Allow", ["logs:CreateLogStream", "logs:PutLogEvents"], logs)]
        if host == "entry":
            rules.append(statement("WebReleaseRead", "Allow", "s3:GetObject", f"{data_arn}/releases/web/*"))
            rules.append(statement("NoParameterReads", "Deny", READ_PARAMETER, "*"))
        elif host == "app":
            rules.extend([
                statement("ListAppPrefixes", "Allow", "s3:ListBucket", data_arn, condition={"StringLike": {"s3:prefix": ["imports/*", "releases/app/*"]}}),
                statement("ImportObjects", "Allow", ["s3:GetObject", "s3:PutObject"], f"{data_arn}/imports/*"),
                statement("AppReleaseRead", "Allow", "s3:GetObject", f"{data_arn}/releases/app/*"),
            ])
        else:
            rules.append(statement("SqlBackups", "Allow", ["s3:GetObject", "s3:PutObject"], f"{data_arn}/backups/sql/*"))
        if host in parameters:
            context = {"StringEquals": {"kms:ViaService": f"ssm.{REGION}.amazonaws.com", "kms:EncryptionContext:PARAMETER_ARN": parameters[host]}}
            rules.extend([
                statement("OwnParameter", "Allow", "ssm:GetParameter", parameters[host]),
                statement("NoOtherParameterReads", "Deny", READ_PARAMETER, not_resources=parameters[host]),
                statement("OwnParameterDecrypt", "Allow", "kms:Decrypt", keys[host], condition=context),
                statement("NoOtherKeyDecrypt", "Deny", "kms:Decrypt", not_resources=keys[host]),
            ])
        else:
            rules.append(statement("NoDecrypt", "Deny", "kms:Decrypt", "*"))
        rules.extend([
            statement("NoParameterEnumerationOrMutation", "Deny", OTHER_PARAMETER, "*"),
            statement("NoIdentityAdministration", "Deny", "iam:*", "*"),
            statement("NoStateAccess", "Deny", "s3:*", [state_arn, f"{state_arn}/*"]),
        ])
        bundles[f"{host}-runtime.json"] = policy(*rules)
        boundary_rules = deepcopy(rules)
        boundary_rules.insert(0, statement("SsmAgentCoreOnly", "Allow", AGENT_ACTIONS, "*"))
        bundles[f"{host}-boundary.json"] = policy(*boundary_rules)

    for host in ["app", "db"]:
        context = {"StringEquals": {"kms:ViaService": f"ssm.{REGION}.amazonaws.com", "kms:EncryptionContext:PARAMETER_ARN": parameters[host]}}
        bundles[f"{host}-key-policy.json"] = policy(
            statement("NamedKeyAdministrator", "Allow", "kms:*", "*", principal={"AWS": config["key_admin_arn"]}),
            statement("RuntimeDecrypt", "Allow", "kms:Decrypt", "*", principal={"AWS": role_arns[host]}, condition=context),
            statement("CustodianEncrypt", "Allow", "kms:Encrypt", "*", principal={"AWS": config[f"{host}_custodian_arn"]}, condition=context),
        )
        bundles[f"{host}-secret-writer.json"] = policy(
            statement("WriteOwnStandardParameter", "Allow", "ssm:PutParameter", parameters[host]),
            statement("EncryptOwnParameter", "Allow", "kms:Encrypt", keys[host], condition=context),
            statement("NoReadBack", "Deny", READ_PARAMETER + ["ssm:GetParametersByPath", "ssm:GetParameterHistory", "kms:Decrypt"], "*"),
            statement("NoOtherParameterWrites", "Deny", "ssm:PutParameter", not_resources=parameters[host]),
            statement("NoOtherKeyEncrypt", "Deny", "kms:Encrypt", not_resources=keys[host]),
        )

    def backend_policy(prefix):
        path = f"{prefix}/terraform.tfstate"
        return policy(
            statement("ListStatePrefix", "Allow", "s3:ListBucket", state_arn, condition={"StringLike": {"s3:prefix": [path, f"{path}.tflock"]}}),
            statement("StateReadWrite", "Allow", ["s3:GetObject", "s3:PutObject"], f"{state_arn}/{path}"),
            statement("LockReadWriteDelete", "Allow", ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"], f"{state_arn}/{path}.tflock"),
        )

    bundles["terraform-backend-passrole.json"] = backend_policy("staging/infrastructure")
    bundles["terraform-backend-passrole.json"]["Statement"].extend([
        statement("OnlyThreeEc2Roles", "Allow", "iam:PassRole", list(role_arns.values()), condition={"StringEquals": {"iam:PassedToService": "ec2.amazonaws.com"}}),
        statement("NoIamMutation", "Deny", ["iam:Create*", "iam:Delete*", "iam:Update*", "iam:Put*", "iam:Attach*", "iam:Detach*", "iam:Add*", "iam:Remove*", "iam:Set*"], "*"),
        statement("NoSecretValues", "Deny", READ_PARAMETER + OTHER_PARAMETER, "*"),
        statement("NoSecretKeyUse", "Deny", ["kms:Decrypt", "kms:Encrypt", "kms:GenerateDataKey*"], list(keys.values())),
        statement("NoKeyPolicyMutation", "Deny", "kms:PutKeyPolicy", "*"),
    ])
    bundles["bootstrap-backend.json"] = backend_policy("bootstrap")

    # Conservative resource-policy draft: tag conditions deliberately fail closed
    # where a dependent EC2 resource does not carry that context. Review action-by-
    # resource authorization against the actual AMI/network before first apply.
    ec2_types = ["vpc", "subnet", "route-table", "internet-gateway", "natgateway", "elastic-ip", "security-group", "instance", "volume", "network-interface", "vpc-endpoint"]
    ec2_resources = [f"arn:aws:ec2:{REGION}:{account}:{kind}/*" for kind in ec2_types]
    region_condition = {"StringEquals": {"aws:RequestedRegion": REGION}}
    tagged_create = {"StringEquals": {"aws:RequestedRegion": REGION, "aws:RequestTag/Project": "AIExamBank", "aws:RequestTag/Environment": "staging"}}
    tagged_existing = {"StringEquals": {"aws:RequestedRegion": REGION, "ec2:ResourceTag/Project": "AIExamBank", "ec2:ResourceTag/Environment": "staging"}}
    bundles["terraform-resources.review.json"] = policy(
        statement("RegionalReadOnlyInventory", "Allow", [
            "ec2:DescribeVpcs", "ec2:DescribeVpcAttribute", "ec2:DescribeSubnets", "ec2:DescribeRouteTables",
            "ec2:DescribeInternetGateways", "ec2:DescribeNatGateways", "ec2:DescribeAddresses",
            "ec2:DescribeSecurityGroups", "ec2:DescribeSecurityGroupRules", "ec2:DescribeInstances",
            "ec2:DescribeInstanceAttribute", "ec2:DescribeInstanceCreditSpecifications", "ec2:DescribeVolumes",
            "ec2:DescribeVpcEndpoints", "ec2:DescribeVpcEndpointServices", "ec2:DescribeImages",
            "ec2:DescribeInstanceTypes", "ec2:DescribeTags", "ec2:DescribeAvailabilityZones",
            "ec2:DescribeNetworkInterfaces", "ec2:DescribePrefixLists", "ec2:DescribeManagedPrefixLists",
        ], "*", condition=region_condition),
        statement("TaggedCreateDraft", "Allow", [
            "ec2:CreateVpc", "ec2:CreateSubnet", "ec2:CreateRouteTable", "ec2:CreateInternetGateway",
            "ec2:AllocateAddress", "ec2:CreateNatGateway", "ec2:CreateVpcEndpoint", "ec2:CreateSecurityGroup",
            "ec2:CreateVolume", "ec2:RunInstances",
        ], ec2_resources + [f"arn:aws:ec2:{REGION}::image/ami-*"], condition=tagged_create),
        statement("TaggedMutationDraft", "Allow", [
            "ec2:ModifyVpcAttribute", "ec2:DeleteVpc", "ec2:ModifySubnetAttribute", "ec2:DeleteSubnet",
            "ec2:AssociateRouteTable", "ec2:DisassociateRouteTable", "ec2:CreateRoute", "ec2:ReplaceRoute", "ec2:DeleteRoute", "ec2:DeleteRouteTable",
            "ec2:AttachInternetGateway", "ec2:DetachInternetGateway", "ec2:DeleteInternetGateway",
            "ec2:AssociateAddress", "ec2:DisassociateAddress", "ec2:ReleaseAddress", "ec2:DeleteNatGateway",
            "ec2:ModifyVpcEndpoint", "ec2:DeleteVpcEndpoints", "ec2:TerminateInstances", "ec2:StartInstances", "ec2:StopInstances",
            "ec2:ModifyInstanceAttribute", "ec2:ModifyInstanceMetadataOptions", "ec2:ModifyInstanceCreditSpecification",
            "ec2:AttachVolume", "ec2:DetachVolume", "ec2:DeleteVolume", "ec2:AuthorizeSecurityGroupIngress",
            "ec2:AuthorizeSecurityGroupEgress", "ec2:RevokeSecurityGroupIngress", "ec2:RevokeSecurityGroupEgress", "ec2:DeleteSecurityGroup",
        ], ec2_resources, condition=tagged_existing),
        statement("CreateResourceTagsOnly", "Allow", "ec2:CreateTags", ec2_resources,
                  condition={"StringEquals": {"aws:RequestedRegion": REGION}, "StringEqualsIfExists": {"aws:RequestTag/Project": "AIExamBank", "aws:RequestTag/Environment": "staging"}, "Null": {"ec2:CreateAction": "false"}}),
        statement("NamedDataBucketControl", "Allow", [
            "s3:CreateBucket", "s3:DeleteBucket", "s3:GetBucketLocation", "s3:ListBucket",
            "s3:GetBucketPolicy", "s3:PutBucketPolicy", "s3:DeleteBucketPolicy",
            "s3:GetBucketVersioning", "s3:PutBucketVersioning", "s3:GetEncryptionConfiguration", "s3:PutEncryptionConfiguration",
            "s3:GetBucketPublicAccessBlock", "s3:PutBucketPublicAccessBlock",
            "s3:GetBucketOwnershipControls", "s3:PutBucketOwnershipControls", "s3:DeleteBucketOwnershipControls",
            "s3:GetBucketTagging", "s3:PutBucketTagging", "s3:GetBucketAcl", "s3:ListBucketVersions", "s3:GetBucketCORS", "s3:GetLifecycleConfiguration",
        ], data_arn, condition=region_condition),
        statement("NamedLogGroupControl", "Allow", ["logs:CreateLogGroup", "logs:DeleteLogGroup", "logs:PutRetentionPolicy", "logs:DeleteRetentionPolicy", "logs:TagResource", "logs:UntagResource", "logs:ListTagsForResource"],
                  [f"arn:aws:logs:{REGION}:{account}:log-group:/aieb/staging/{group}{suffix}" for group in ["entry", "app", "db", "ssm-sessions"] for suffix in ["", ":*"]]),
        statement("LogGroupDiscovery", "Allow", "logs:DescribeLogGroups", "*", condition=region_condition),
        statement("ReadApprovedProfiles", "Allow", "iam:GetInstanceProfile", [f"arn:aws:iam::{account}:instance-profile/aieb-staging-{host}-profile" for host in role_arns]),
    )

    for name, hosts, prefix in [
        ("operator", ["entry", "app"], config["operator_session_prefix"]),
        ("dba", ["db"], config["dba_session_prefix"]),
    ]:
        instances = [f"arn:aws:ec2:{REGION}:{account}:instance/{config[host + '_instance_id']}" for host in hosts]
        own_session = f"arn:aws:ssm:{REGION}:{account}:session/{prefix}-*"
        bundles[f"{name}-sessions.json"] = policy(
            statement("NamedHostsAndShellDocument", "Allow", "ssm:StartSession", instances + [f"arn:aws:ssm:{REGION}:{account}:document/SSM-SessionManagerRunShell"]),
            statement("OwnSessionsOnly", "Allow", ["ssm:ResumeSession", "ssm:TerminateSession"], own_session),
            statement("OwnDataChannel", "Allow", "ssmmessages:OpenDataChannel", own_session),
            statement("NoRunCommand", "Deny", "ssm:SendCommand", "*"),
        )

    bundles["session-preferences.json"] = {
        "schemaVersion": "1.0", "description": "Staging shell session logging",
        "sessionType": "Standard_Stream",
        "inputs": {
            "cloudWatchLogGroupName": SESSION_LOG,
            "cloudWatchEncryptionEnabled": False,
            "cloudWatchStreamingEnabled": True,
            "s3BucketName": "", "s3KeyPrefix": "", "s3EncryptionEnabled": True,
            "kmsKeyId": "", "runAsEnabled": False, "runAsDefaultUser": "",
            "idleSessionTimeout": "20", "maxSessionDuration": "60",
            "shellProfile": {"windows": "", "linux": ""},
        },
    }
    return bundles


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    bundle = build_bundle(json.loads(args.inputs.read_text(encoding="utf-8-sig")))
    args.output.mkdir(parents=True, exist_ok=True)
    for name, document in bundle.items():
        path = args.output / name
        if path.exists():
            raise FileExistsError(f"Refusing to overwrite an existing policy: {path}")
    for name, document in bundle.items():
        (args.output / name).write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    print(f"Rendered {len(bundle)} review templates. No AWS call or deployment was performed.")


if __name__ == "__main__":
    main()
