{ pkgs, pkgs-2305, atomi, pkgs-feb-23-24 }:
let
  all = {
    atomipkgs = (
      with atomi;
      {
        inherit
          infisical
          mirrord
          sg
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
    feb-23-24 = (
      with pkgs-feb-23-24;
      {
        helm = kubernetes-helm;
        inherit
          skopeo
          doppler
          coreutils
          yq-go
          gnused
          dotnet-sdk_8
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
feb-23-24

