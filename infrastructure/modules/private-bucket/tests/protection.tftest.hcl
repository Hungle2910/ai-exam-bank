mock_provider "aws" {}
variables {
  bucket_name = "aieb-local-mock-state"
  versioning  = true
}

run "state_protection" {
  command = plan
  assert {
    condition = (
      aws_s3_bucket_public_access_block.this.block_public_acls &&
      aws_s3_bucket_public_access_block.this.block_public_policy &&
      aws_s3_bucket_public_access_block.this.ignore_public_acls &&
      aws_s3_bucket_public_access_block.this.restrict_public_buckets &&
      !aws_s3_bucket.this.force_destroy
    )
    error_message = "Public access and force-destroy must remain disabled."
  }
  assert {
    condition     = aws_s3_bucket_versioning.this.versioning_configuration[0].status == "Enabled"
    error_message = "State history must remain enabled."
  }
}
