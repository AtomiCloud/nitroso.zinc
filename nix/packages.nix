{ pkgs, pkgs-2305, atomi, atomi_classic, pkgs-nov-22-23, pkgs-dotnet8 }:
let

  all = {
    atomipkgs_classic = (
      with atomi_classic;
      {
        inherit
          sg;
      }
    );
    atomipkgs = (
      with atomi;
      {
        inherit
          infisical
          mirrord
          pls;
      }
    );
    nix-2305 = (
      with pkgs-2305;
      {
        inherit
          tilt
          hadolint;
      }
    );
    dotnet8 = (
      with pkgs-dotnet8;
      {
        inherit
          dotnet-sdk_8;
      }
    );
    nov-22-23 = (
      with pkgs-nov-22-23;
      {
        nodejs = nodejs_20;
        helm = kubernetes-helm;
        npm = nodePackages.npm;
        inherit
          skopeo
          doppler
          coreutils
          yq-go
          gnused
          gnugrep
          bash
          jq
          findutils

          git


          # infra
          docker
          k3d

          kubectl

          # linter
          treefmt
          gitlint
          shellcheck
          helm-docs
          ;
      }
    );
  };
in
with all;
nix-2305 //
atomipkgs //
atomipkgs_classic //
nov-22-23 //
dotnet8 

