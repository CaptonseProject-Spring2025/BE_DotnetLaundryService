# ---------- Build Stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution và tất cả các project vào image
COPY ../LaundryService.sln ./
COPY ../LaundryService.Api/*.csproj ./LaundryService.Api/
COPY ../LaundryService.Domain/*.csproj ./LaundryService.Domain/
COPY ../LaundryService.Dto/*.csproj ./LaundryService.Dto/
COPY ../LaundryService.Infrastructure/*.csproj ./LaundryService.Infrastructure/
COPY ../LaundryService.Service/*.csproj ./LaundryService.Service/

# Restore các dependency từ nuget
RUN dotnet restore

# Copy toàn bộ source code vào image (sau khi restore để cache hiệu quả)
COPY ../ ./

# Publish project ở chế độ Release
RUN dotnet publish LaundryService.Api/LaundryService.Api.csproj -c Release -o /app/publish

# ---------- Runtime Stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# >>> THÊM FONT + fontconfig <<<
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      fontconfig \
      fonts-dejavu-core \
      fonts-dejavu-extra \
 && fc-cache -fv \
 && rm -rf /var/lib/apt/lists/*

# Copy các file publish từ build stage
COPY --from=build /app/publish .

# Copy file firebase config vào container nếu cần
COPY LaundryService.Api/notification-laundry-firebase-adminsdk.json ./

# Expose port mặc định
EXPOSE 8080

# Command để chạy ứng dụng
ENTRYPOINT ["dotnet", "LaundryService.Api.dll"]