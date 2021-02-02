FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build-env

WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY *.csproj ./

RUN dotnet restore

COPY . ./

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime

WORKDIR /app

FROM mcr.microsoft.com/dotnet/runtime:3.1

COPY --from=build-env /app/out .

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update

RUN apt-get install -y krb5-config

RUN apt-get install -y krb5-user

RUN mkdir /krb5 && chmod 755 /krb5

VOLUME ["/krb5","/dev/shm","/etc/krb5.conf.d"]

ENTRYPOINT ["dotnet", "GoSql.Kerberos.Console.dll"]