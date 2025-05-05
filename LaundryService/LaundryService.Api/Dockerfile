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

# Cập nhật danh sách gói và cài đặt các gói font cần thiết
# fonts-dejavu-core: Cung cấp font DejaVu Sans
# ttf-mscorefonts-installer: Cung cấp các font Microsoft phổ biến (Arial, Times New Roman, v.v.) - rất hữu ích làm fallback
# fontconfig: Công cụ quản lý font, bao gồm fc-cache
RUN sed -i 's/Components: main/Components: main contrib/' /etc/apt/sources.list.d/debian.sources && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
        fonts-dejavu-core \
        fontconfig \
        # ttf-mscorefonts-installer có thể cần wget và ca-certificates để tải font
        wget \
        ca-certificates \
    # Chấp nhận EULA cho font Microsoft trước khi cài đặt
    && echo ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true | debconf-set-selections \
    && apt-get install -y --no-install-recommends ttf-mscorefonts-installer \
    # Cập nhật font cache của hệ thống
    && fc-cache -fv && \
    # Dọn dẹp sau khi cài đặt để giảm kích thước image
    # Xóa các gói không cần thiết sau khi cài font (wget, ca-certificates)
    && apt-get purge -y --auto-remove wget ca-certificates && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy các file publish từ build stage
COPY --from=build /app/publish .

# Copy file firebase config vào container nếu cần
COPY LaundryService.Api/notification-laundry-firebase-adminsdk.json ./

# Expose port mặc định
EXPOSE 8080

# Command để chạy ứng dụng
ENTRYPOINT ["dotnet", "LaundryService.Api.dll"]