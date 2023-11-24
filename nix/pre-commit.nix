{ packages, formatter, pre-commit-lib }:
pre-commit-lib.run {
  src = ./.;

  # hooks
  hooks = {
    # formatter
    treefmt = {
      enable = true;
      excludes = [
        "infra/.*chart.*/templates/.*(yaml|yml)"
        "infra/.*chart.*/.*(MD|md)"
        ".*(Changelog|README).+(MD|md)"
      ];
    };

    # linters From https://github.com/cachix/pre-commit-hooks.nix
    shellcheck = {
      enable = false;
    };

    a-config-sync = {
      enable = true;
      name = "Sync configurations to helm charts";
      entry = "${packages.bash}/bin/bash scripts/local/config-sync.sh";
      files = "App/Config/.*\\.yaml";
      language = "system";
      pass_filenames = false;
    };

    a-helm-lint-api-chart = {
      enable = true;
      name = "Helm Lint API Chart";
      description = "Lints helm API charts";
      entry = "${packages.helm}/bin/helm lint -f infra/api_chart/values.yaml infra/api_chart";
      files = "infra/api_chart/.*";
      language = "system";
      pass_filenames = false;
    };

    a-helm-lint-migration-chart = {
      enable = true;
      name = "Helm Lint Migration Chart";
      description = "Lints helm migration charts";
      entry = "${packages.helm}/bin/helm lint -f infra/migration_chart/values.yaml infra/migration_chart";
      files = "infra/migration_chart/.*";
      language = "system";
      pass_filenames = false;
    };

    a-helm-lint-root-chart = {
      enable = true;
      name = "Helm Lint Root Chart";
      description = "Lints helm root charts";
      entry = "${packages.helm}/bin/helm lint -f infra/root_chart/values.yaml infra/root_chart";
      files = "infra/root_chart/.*";
      language = "system";
      pass_filenames = false;
    };

    a-dotnet-fmt-app = {
      enable = true;
      name = "Format .NET 'App' Project";
      description = "Run formatter for .NET Project 'App'";
      entry = "${packages.dotnet-sdk_8}/bin/dotnet format whitespace --no-restore -v d ./App/App.csproj";
      language = "system";
      pass_filenames = false;
      files = "^App/.*\\.cs$";
    };

    a-dotnet-lint-app = {
      enable = true;
      name = "Lint .NET 'App' Project";
      description = "Run formatter for .NET Project 'App'";
      entry = "${packages.dotnet-sdk_8}/bin/dotnet format style --no-restore --severity info --verify-no-changes -v d ./App/App.csproj";
      language = "system";
      pass_filenames = false;
      files = "^App/.*\\.cs$";
    };

    a-dotnet-fmt-domain = {
      enable = true;
      name = "Format .NET 'Domain' Project";
      description = "Run formatter for .NET Project 'Domain'";
      entry = "${packages.dotnet-sdk_8}/bin/dotnet format whitespace --no-restore -v d ./Domain/Domain.csproj";
      language = "system";
      pass_filenames = false;
      files = "^Domain/.*\\.cs$";
    };

    a-dotnet-lint-domain = {
      enable = true;
      name = "Lint .NET 'Domain' Project";
      description = "Run formatter for .NET Project 'Domain'";
      entry = "${packages.dotnet-sdk_8}/bin/dotnet format style --no-restore --severity info --verify-no-changes -v d ./Domain/Domain.csproj";
      language = "system";
      pass_filenames = false;
      files = "^Domain/.*\\.cs$";
    };

    a-dotnet-fmt-unit = {
      enable = true;
      name = "Format .NET 'UnitTest' Project";
      description = "Run formatter for .NET Project 'UnitTest'";
      entry = "${packages.dotnet-sdk_8}/bin/dotnet format whitespace --no-restore -v d ./UnitTest/UnitTest.csproj";
      language = "system";
      pass_filenames = false;
      files = "^UnitTest/.*\\.cs$";
    };

    a-dotnet-lint-unit = {
      enable = true;
      name = "Lint .NET 'UnitTest' Project";
      description = "Run formatter for .NET Project 'UnitTest'";
      entry = "${packages.dotnet-sdk_8}/bin/dotnet format style --no-restore --severity info --verify-no-changes -v d ./UnitTest/UnitTest.csproj";
      language = "system";
      pass_filenames = false;
      files = "^UnitTest/.*\\.cs$";
    };

    a-infisical = {
      enable = true;
      name = "Secrets Scanning";
      description = "Scan for possible secrets";
      entry = "${packages.infisical}/bin/infisical scan . -v";
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
      entry = "${packages.coreutils}/bin/chmod +x";
      files = ".*sh$";
      language = "system";
      pass_filenames = true;
    };

    a-hadolint = {
      enable = true;
      name = "Docker Linter";
      entry = "${packages.hadolint}/bin/hadolint";
      files = ".*Dockerfile$";
      language = "system";
      pass_filenames = true;
    };

    a-helm-docs = {
      enable = true;
      name = "Helm Docs";
      entry = "${packages.helm-docs}/bin/helm-docs";
      files = ".*";
      language = "system";
      pass_filenames = false;
    };

  };

  settings = {
    treefmt = {
      package = formatter;
    };
  };
}
