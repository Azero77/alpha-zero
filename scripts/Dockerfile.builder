FROM amazonlinux:2023
RUN dnf install -y tar gzip clang krb5-devel zlib-devel libicu
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin -c 10.0 --install-dir /usr/share/dotnet
ENV PATH="$PATH:/usr/share/dotnet"
