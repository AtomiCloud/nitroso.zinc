{ packages, formatter, pre-commit-lib }:
pre-commit-lib.run {
  src = ./.;

  # hooks
  hooks = {
    # formatter
    treefmt = {
      enable = true;
      package = formatter;
      excludes = [
        "infra/.*chart.*/templates/.*(yaml|yml)"
        "infra/.*chart.*/.*(MD|md)"
        ".*(Changelog|README|CommitConventions).+(MD|md)"
        ".*schema.json"
      ];
    };
    # linters From https://github.com/cachix/pre-commit-hooks.nix
    shellcheck.enable = false;


    a-config-sync = rec {
      enable = true;
      name = "Sync configurations to helm charts";
      entry = "${packages.atomiutils}/bin/bash scripts/local/config-sync.sh";
      files = "App/Config/.*\\.yaml";
      language = "system";
      pass_filenames = false;
    };

    a-helm-lint = rec {
      enable = true;
      name = "Lint Helm Charts";
      description = "Lints helm charts";
      entry = "${packages.infralint}/bin/helmlint";
      files = "infra/.*";
      language = "system";
      pass_filenames = false;
    };

    # custom precommits 
    a-dotnet-lint = {
      enable = true;
      name = "Lint .NET";
      description = "Run linter for .NET Projects'";
      entry = "${packages.dotnetlint}/bin/dotnetlint";
      language = "system";
      pass_filenames = false;
      files = "^.*\\.cs$";
    };

    a-infisical = {
      enable = true;
      name = "Secrets Scanning";
      description = "Scan for possible secrets";
      entry = "${packages.infisical}/bin/infisical scan . --verbose";
      language = "system";
      pass_filenames = false;
    };

    a-infisical-staged = {
      enable = true;
      name = "Secrets Scanning (Staged files)";
      description = "Scan for possible secrets in staged files";
      entry = "${packages.infisical}/bin/infisical scan git-changes --staged -v";
      language = "system";
      pass_filenames = false;
    };

    a-gitlint = {
      enable = true;
      name = "Gitlint";
      description = "Lints git commit message";
      entry = "${packages.gitlint}/bin/gitlint --staged --msg-filename .git/COMMIT_EDITMSG";
      language = "system";
      pass_filenames = false;
      stages = [ "commit-msg" ];
    };

    a-enforce-gitlint = {
      enable = true;
      name = "Enforce gitlint";
      description = "Enforce atomi_releaser conforms to gitlint";
      entry = "${packages.sg}/bin/sg gitlint";
      files = "(atomi_release\\.yaml|\\.gitlint)";
      language = "system";
      pass_filenames = false;
    };

    a-shellcheck = {
      enable = true;
      name = "Shell Check";
      entry = "${packages.shellcheck}/bin/shellcheck";
      files = ".*sh$";
      language = "system";
      pass_filenames = true;
    };

    a-enforce-exec = {
      enable = true;
      name = "Enforce Shell Script executable";
      entry = "${packages.atomiutils}/bin/chmod +x";
      files = ".*sh$";
      language = "system";
      pass_filenames = true;
    };

    /*
      
    */
    a-hadolint = {
      enable = true;
      name = "Docker Linter";
      entry = "${packages.infralint}/bin/hadolint";
      files = ".*Dockerfile$";
      language = "system";
      pass_filenames = true;
    };

    a-helm-docs = {
      enable = true;
      name = "Helm Docs";
      entry = "${packages.infralint}/bin/helm-docs";
      files = ".*";
      language = "system";
      pass_filenames = false;
    };

  };

}
