{ pkgs, packages }:
with packages;
{
  system = [
    coreutils
    findutils
    gnugrep
    gnused
    yq-go
    jq
  ];

  dev = [
    skopeo
    pls
    git
    doppler
  ];

  infra = [
    docker
    k3d
    helm
    kubectl
    tilt
    mirrord
  ];

  main = [
    dotnet-sdk_8
    infisical
  ];

  lint = [
    # core
    treefmt
    gitlint
    hadolint
    shellcheck
    helm-docs
    sg
    docker
    helm
  ];

  releaser = [
    sg
  ];
}
