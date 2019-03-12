#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see http://aka.ms/containercompat 

#choose runtime as needed.  Ideally, the maintainer will make a dockerfile for each runtime, build each image and push them all to docker.io repository
#https://hub.docker.com/r/microsoft/nanoserver/
#https://hub.docker.com/_/dotnet
#https://blogs.msdn.microsoft.com/dotnet/2018/06/18/staying-up-to-date-with-net-container-images/
#https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/net-core-net-framework-containers/net-container-os-targets

#FROM microsoft/dotnet:2.1-runtime AS runtime
#FROM mcr.microsoft.com/windows/nanoserver:sac2016 AS runtime
#FROM mcr.microsoft.com/windows/nanoserver:1809 AS runtime
FROM microsoft/dotnet:2.1-aspnetcore-runtime-nanoserver-1809 AS runtime

WORKDIR /ACE

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY ACE.Server/ACE.Server.csproj ACE.Server/
COPY ACE.DatLoader/ACE.DatLoader.csproj ACE.DatLoader/
COPY ACE.Entity/ACE.Entity.csproj ACE.Entity/
COPY ACE.Common/ACE.Common.csproj ACE.Common/
COPY ACE.Database/ACE.Database.csproj ACE.Database/
RUN dotnet restore ACE.Server/ACE.Server.csproj
COPY . .
WORKDIR /src/ACE.Server
RUN dotnet build ACE.Server.csproj -c Release -o /ACE

FROM build AS publish
RUN dotnet publish ACE.Server.csproj -c Release -o /ACE

FROM runtime AS final
WORKDIR /ACE
COPY --from=publish /ACE .

EXPOSE 9000/udp
EXPOSE 9001/udp

ENTRYPOINT ["dotnet", "ACE.Server.dll"]
