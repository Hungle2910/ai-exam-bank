"""Regression cases for real repository hygiene failures."""
import tempfile
import unittest
from pathlib import Path

from tools.check_repository import inspect_file, inspect_module_ownership


class RepositoryChecksTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def write(self, name, content):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding='utf-8')
        return Path(name)

    def test_valid_unicode_local_link_and_external_link(self):
        self.write('docs/hướng dẫn.md', '# Guide')
        file = self.write('README.md', '[Guide](<docs/hướng dẫn.md>) [Web](https://example.com)')
        self.assertEqual(inspect_file(self.root, file), [])

    def test_missing_file_fails(self):
        file = self.write('README.md', '[Missing](docs/missing.md)')
        self.assertTrue(inspect_file(self.root, file))

    def test_outside_repository_link_fails(self):
        file = self.write('README.md', '[Outside](../outside.md)')
        self.assertTrue(inspect_file(self.root, file))

    def test_fenced_example_is_not_a_real_link(self):
        file = self.write('README.md', '```markdown\n[Example](not-real.md)\n```\n')
        self.assertEqual(inspect_file(self.root, file), [])

    def test_runtime_environment_is_rejected_but_example_allowed(self):
        self.assertTrue(inspect_file(self.root, self.write('.env.production', 'MODE=demo')))
        self.assertEqual(inspect_file(self.root, self.write('.env.example', 'MODE=demo')), [])

    def test_private_key_detection_does_not_echo_contents(self):
        marker = '-----BEGIN ' + 'PRIVATE KEY-----'
        file = self.write('accidental.txt', marker + '\nnot-a-real-key\n')
        issues = inspect_file(self.root, file)
        self.assertTrue(issues)
        self.assertNotIn('not-a-real-key', '\n'.join(issues))

    def test_runtime_config_formats_and_safe_examples(self):
        for name in ['credentials', 'appsettings.Local.json',
                     'appsettings.Development.Local.json', 'production.tfvars',
                     'production.tfvars.json', 'production.auto.tfvars.json']:
            with self.subTest(name=name):
                self.assertTrue(inspect_file(self.root, self.write(name, '{}')))
        for name in ['production.tfvars.example', 'production.tfvars.json.example',
                     'appsettings.Local.json.example']:
            with self.subTest(name=name):
                self.assertEqual(inspect_file(self.root, self.write(name, '{}')), [])

    def test_module_review_routing_matches_documented_owner(self):
        self.write('.github/CODEOWNERS', '/src/Modules/Knowledge/ @flwndyy @Hungle2910\n')
        self.write('src/Modules/README.md',
                   '| Module | Primary owner | Review partner |\n'
                   '|---|---|---|\n'
                   '| Knowledge | @flwndyy | @Lancelot-sys25 |\n')
        self.assertEqual(len(inspect_module_ownership(self.root)), 1)
        self.write('src/Modules/README.md',
                   '| Module | Primary owner | Review partner |\n'
                   '|---|---|---|\n'
                   '| Knowledge | @flwndyy | @Hungle2910 |\n')
        self.assertEqual(inspect_module_ownership(self.root), [])


if __name__ == '__main__':
    unittest.main()
