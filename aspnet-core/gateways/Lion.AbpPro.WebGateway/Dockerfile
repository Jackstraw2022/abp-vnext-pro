FROM mcr.microsoft.com/dotnet/aspnet:9.0

# 创建目录
RUN mkdir /app

COPY publish /app

# 设置工作目录
WORKDIR /app

# 暴露8080端口
EXPOSE 8080

# 设置时区 .net6 才有这个问题
ENV TZ=Asia/Shanghai

# 设置环境变量
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Lion.AbpPro.WebGateway.dll"]


