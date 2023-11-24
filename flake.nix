{
  inputs = {
    # util
    flake-utils.url = "github:numtide/flake-utils";
    treefmt-nix.url = "github:numtide/treefmt-nix";
    pre-commit-hooks.url = "github:cachix/pre-commit-hooks.nix";

    # registry
    nixpkgs.url = "nixpkgs/78058d810644f5ed276804ce7ea9e82d92bee293";
    nixpkgs-2305.url = "nixpkgs/nixos-23.05";
    nixpkgs-nov-22-23.url = "nixpkgs/e4ad989506ec7d71f7302cc3067abd82730a4beb";
    nixpkgs-dotnet8.url = "nixpkgs/aa7e324b9168c1b2e15698d6e53d22f4f98634f1";
    atomipkgs.url = "github:kirinnee/test-nix-repo/v22.0.1";
    atomipkgs_classic.url = "github:kirinnee/test-nix-repo/classic";

  };
  outputs =
    { self

      # utils
    , flake-utils
    , treefmt-nix
    , pre-commit-hooks

      # registries
    , atomipkgs
    , atomipkgs_classic
    , nixpkgs
    , nixpkgs-2305
    , nixpkgs-nov-22-23
    , nixpkgs-dotnet8

    } @inputs:
    (flake-utils.lib.eachDefaultSystem
      (
        system:
        let
          pkgs = nixpkgs.legacyPackages.${system};
          pkgs-2305 = nixpkgs-2305.legacyPackages.${system};
          pkgs-nov-22-23 = nixpkgs-nov-22-23.legacyPackages.${system};
          pkgs-dotnet8 = nixpkgs-dotnet8.legacyPackages.${system};
          atomi = atomipkgs.packages.${system};
          atomi_classic = atomipkgs_classic.packages.${system};
          pre-commit-lib = pre-commit-hooks.lib.${system};
        in
        with rec {
          pre-commit = import ./nix/pre-commit.nix {
            inherit packages pre-commit-lib formatter;
          };
          formatter = import ./nix/fmt.nix {
            inherit treefmt-nix pkgs;
          };
          packages = import ./nix/packages.nix
            {
              inherit pkgs pkgs-2305 atomi atomi_classic pkgs-nov-22-23 pkgs-dotnet8;
            };
          env = import ./nix/env.nix {
            inherit pkgs packages;
          };
          devShells = import ./nix/shells.nix {
            inherit pkgs env packages;
            shellHook = checks.pre-commit-check.shellHook;
          };
          checks = {
            pre-commit-check = pre-commit;
            format = formatter;
          };
        };
        {
          inherit checks formatter packages devShells;
        }
      )
    )
  ;

}
