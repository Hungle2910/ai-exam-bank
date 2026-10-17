"""Offline regression checks for the policy generator, not AWS IAM simulation."""
from copy import deepcopy
from fnmatch import fnmatchcase
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("m2_policies", ROOT / "infrastructure/security/render_policies.py")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


def statements_for(document, action, resource, context):
    """Match only the emitted subset. Resource policies, SCPs and grants are not modeled."""
    for rule in document["Statement"]:
        actions = rule["Action"] if isinstance(rule["Action"], list) else [rule["Action"]]
        if not any(fnmatchcase(action.lower(), item.lower()) for item in actions):
            continue
        resources = rule.get("Resource", rule.get("NotResource", []))
        resources = resources if isinstance(resources, list) else [resources]
        matches = any(fnmatchcase(resource, item) for item in resources)
        if ("NotResource" in rule and matches) or ("Resource" in rule and not matches):
            continue
        accepted = True
        for operator, constraints in rule.get("Condition", {}).items():
            for name, values in constraints.items():
                values = values if isinstance(values, list) else [values]
                if operator == "StringEquals":
                    accepted &= context.get(name) in values
                elif operator == "StringLike":
                    accepted &= any(fnmatchcase(context.get(name, ""), value) for value in values)
                else:
                    raise AssertionError(f"Unsupported condition in offline matcher: {operator}")
        if accepted:
            yield rule


def decision(documents, action, resource, context=None):
    matches = [rule for doc in documents for rule in statements_for(doc, action, resource, context or {})]
    if any(rule["Effect"] == "Deny" for rule in matches):
        return "explicitDeny"
    return "allowed" if any(rule["Effect"] == "Allow" for rule in matches) else "implicitDeny"


class M2PolicyTemplateTests(unittest.TestCase):
    def setUp(self):
        self.config = json.loads((ROOT / "infrastructure/security/policy-inputs.example.json").read_text())
        self.bundle = generator.build_bundle(self.config)
        self.account = self.config["account_id"]
        self.param = lambda host: f"arn:aws:ssm:ap-southeast-1:{self.account}:parameter/aieb/staging/{host}/" + ("connection-string" if host == "app" else "bootstrap-password")
        self.key = lambda host: f"arn:aws:kms:ap-southeast-1:{self.account}:key/{self.config[host + '_kms_key_id']}"
        self.managed_reads = generator.policy(generator.statement("ManagedCoreParameterRead", "Allow", generator.READ_PARAMETER, "*"))

    def test_managed_core_union_cannot_read_other_secret(self):
        for host in ["entry", "app", "db"]:
            docs = [self.managed_reads, self.bundle[f"{host}-runtime.json"]]
            for target in ["app", "db"]:
                expected = "allowed" if host == target else "explicitDeny"
                for action in generator.READ_PARAMETER:
                    self.assertEqual(decision(docs, action, self.param(target)), expected)
            for action in ["ssm:GetParametersByPath", "ssm:GetParameterHistory"]:
                self.assertEqual(decision(docs, action, self.param("app")), "explicitDeny")

    def test_kms_checks_exact_key_context_and_service(self):
        for host in ["app", "db"]:
            doc = self.bundle[f"{host}-runtime.json"]
            context = {"kms:ViaService": "ssm.ap-southeast-1.amazonaws.com", "kms:EncryptionContext:PARAMETER_ARN": self.param(host)}
            self.assertEqual(decision([doc], "kms:Decrypt", self.key(host), context), "allowed")
            self.assertEqual(decision([doc], "kms:Decrypt", self.key(host), {}), "implicitDeny")
            context["kms:EncryptionContext:PARAMETER_ARN"] = self.param("db" if host == "app" else "app")
            self.assertEqual(decision([doc], "kms:Decrypt", self.key(host), context), "implicitDeny")
            self.assertEqual(decision([doc], "kms:Decrypt", self.key("db" if host == "app" else "app"), context), "explicitDeny")

    def test_prefixes_and_state_are_isolated(self):
        base = f"arn:aws:s3:::aieb-staging-data-{self.account}-ap-southeast-1"
        app = self.bundle["app-runtime.json"]
        self.assertEqual(decision([app], "s3:PutObject", base + "/imports/batch/file.csv"), "allowed")
        self.assertEqual(decision([app], "s3:GetObject", base + "/backups/sql/backup.bak"), "implicitDeny")
        self.assertEqual(decision([app], "s3:PutObject", base + "/releases/app/build.zip"), "implicitDeny")
        for host in ["entry", "app", "db"]:
            self.assertEqual(decision([self.bundle[f"{host}-runtime.json"]], "s3:GetObject", f"arn:aws:s3:::aieb-tfstate-{self.account}-ap-southeast-1/staging/infrastructure/terraform.tfstate"), "explicitDeny")

    def test_backend_cannot_delete_state_or_pass_arbitrary_roles(self):
        doc = self.bundle["terraform-backend-passrole.json"]
        state = f"arn:aws:s3:::aieb-tfstate-{self.account}-ap-southeast-1/staging/infrastructure/terraform.tfstate"
        self.assertEqual(decision([doc], "s3:DeleteObject", state), "implicitDeny")
        self.assertEqual(decision([doc], "s3:DeleteObject", state + ".tflock"), "allowed")
        context = {"iam:PassedToService": "ec2.amazonaws.com"}
        self.assertEqual(decision([doc], "iam:PassRole", f"arn:aws:iam::{self.account}:role/aieb-staging-app-role", context), "allowed")
        self.assertEqual(decision([doc], "iam:PassRole", f"arn:aws:iam::{self.account}:role/Administrator", context), "implicitDeny")
        self.assertEqual(decision([doc], "iam:CreateRole", "*"), "explicitDeny")
        self.assertEqual(decision([doc], "ssm:PutParameter", self.param("app")), "explicitDeny")

    def test_operator_and_dba_host_boundaries(self):
        arn = lambda name: f"arn:aws:ec2:ap-southeast-1:{self.account}:instance/{self.config[name + '_instance_id']}"
        self.assertEqual(decision([self.bundle["operator-sessions.json"]], "ssm:StartSession", arn("app")), "allowed")
        self.assertEqual(decision([self.bundle["operator-sessions.json"]], "ssm:StartSession", arn("db")), "implicitDeny")
        self.assertEqual(decision([self.bundle["dba-sessions.json"]], "ssm:StartSession", arn("db")), "allowed")
        self.assertEqual(decision([self.bundle["dba-sessions.json"]], "ssm:StartSession", arn("entry")), "implicitDeny")
        other = f"arn:aws:ssm:ap-southeast-1:{self.account}:session/someone-else-123"
        self.assertEqual(decision([self.bundle["operator-sessions.json"]], "ssm:TerminateSession", other), "implicitDeny")

    def test_secret_writers_have_no_readback(self):
        for host in ["app", "db"]:
            doc = self.bundle[f"{host}-secret-writer.json"]
            other = "db" if host == "app" else "app"
            self.assertEqual(decision([doc], "ssm:PutParameter", self.param(host)), "allowed")
            self.assertEqual(decision([doc], "ssm:PutParameter", self.param(other)), "explicitDeny")
            self.assertEqual(decision([doc], "ssm:GetParameter", self.param(host)), "explicitDeny")
            self.assertEqual(decision([doc], "kms:Decrypt", self.key(host)), "explicitDeny")

    def test_boundaries_cap_managed_and_future_identity_policies(self):
        app = self.bundle["app-boundary.json"]
        self.assertEqual(decision([app], "ssm:GetParameter", self.param("app")), "allowed")
        self.assertEqual(decision([app], "ssm:GetParameter", self.param("db")), "explicitDeny")
        self.assertEqual(decision([app], "ssm:GetParameters", self.param("app")), "implicitDeny")
        self.assertEqual(decision([app], "iam:PassRole", "*"), "explicitDeny")
        self.assertEqual(decision([app], "ssmmessages:OpenDataChannel", "*"), "allowed")
        policy = self.bundle["app-key-policy.json"]
        runtime = next(s for s in policy["Statement"] if s["Sid"] == "RuntimeDecrypt")
        self.assertEqual(runtime["Principal"]["AWS"], f"arn:aws:iam::{self.account}:role/aieb-staging-app-role")
        self.assertEqual(runtime["Action"], "kms:Decrypt")

    def test_inputs_reject_wildcard_and_combined_keys(self):
        for field, value in [("account_id", "*"), ("app_kms_key_id", self.config["db_kms_key_id"]), ("deployment_principal_arn", "*"), ("operator_session_prefix", "*")]:
            config = deepcopy(self.config)
            config[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                generator.build_bundle(config)

    def test_json_and_session_group_match_terraform(self):
        for name, document in self.bundle.items():
            self.assertEqual(json.loads(json.dumps(document)), document, name)
        storage = (ROOT / "infrastructure/environments/staging/storage.tf").read_text()
        self.assertIn('"ssm-sessions"', storage)
        self.assertEqual(self.bundle["session-preferences.json"]["inputs"]["cloudWatchLogGroupName"], "/aieb/staging/ssm-sessions")


if __name__ == "__main__":
    unittest.main()
