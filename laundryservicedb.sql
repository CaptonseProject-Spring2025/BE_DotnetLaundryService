DO $$ 
DECLARE 
    r RECORD;
BEGIN 
    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP 
        EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
    END LOOP; 
END $$;
-------------------------------------------

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE Users (
    UserId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    FullName TEXT,
    Email TEXT UNIQUE,
    EmailConfirmed BOOLEAN DEFAULT FALSE,
    Password TEXT NOT NULL,
    Status TEXT,
    Role TEXT,
    Avatar TEXT,
    Dob DATE,
    Gender TEXT,
    PhoneNumber TEXT UNIQUE,
	RewardPoints INT DEFAULT 0,
    DateCreated TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    DateModified TIMESTAMP WITH TIME ZONE,
    RefreshToken TEXT,
    RefreshTokenExpiryTime TIMESTAMP WITH TIME ZONE
);

CREATE TABLE Addresses (
    AddressId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserId UUID NOT NULL REFERENCES Users(UserId),
	AddressLabel TEXT,
    ContactName TEXT,
	ContactPhone TEXT,
    DetailAddress TEXT,
	Description TEXT,
	Latitude DECIMAL(9,6),
	Longitude DECIMAL(9,6),
    DateCreated TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    DateModified TIMESTAMP WITH TIME ZONE
);

CREATE TABLE Notifications (
    NotificationId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserId UUID NOT NULL REFERENCES Users(UserId),
    Title TEXT NOT NULL,
    Message TEXT NOT NULL,
    NotificationType TEXT,
    IsRead BOOLEAN DEFAULT FALSE,
	CustomerId UUID,
	OrderId TEXT,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    IsPushEnabled BOOLEAN
);

CREATE TABLE Conversation (
    ConversationID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserOne UUID NOT NULL REFERENCES Users(UserId),
    UserTwo UUID NOT NULL REFERENCES Users(UserId),
    CreationDate TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    Status TEXT --CHECK (Status IN ('active', 'archived', 'deleted'))
);

CREATE TABLE Message (
    MessageID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ConversationID UUID NOT NULL REFERENCES Conversation(ConversationID),
    UserID UUID NOT NULL REFERENCES Users(UserId),
    Message TEXT,
    TypeIs TEXT, --CHECK (TypeIs IN ('text', 'image', 'video', 'file')) DEFAULT 'text',
    ImageLink TEXT,
    CreationDate TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    IsSent BOOLEAN DEFAULT TRUE,
    IsSeen BOOLEAN DEFAULT FALSE,
    Status TEXT --CHECK (Status IN ('normal', 'deleted')) DEFAULT 'normal'
);

CREATE TABLE ServiceCategories (
    CategoryID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name TEXT NOT NULL UNIQUE,  -- Lưu tên danh mục dịch vụ 
    Icon TEXT,  -- Đường dẫn icon hiển thị trên app
	Banner TEXT,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE SubServices (
    SubServiceID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CategoryID UUID NOT NULL REFERENCES ServiceCategories(CategoryID),
    Name TEXT NOT NULL,  -- Tên dịch vụ con
    Description TEXT,  -- Mô tả chi tiết dịch vụ
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE ServiceDetails (
    ServiceID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    SubServiceID UUID NOT NULL REFERENCES SubServices(SubServiceID),
    Name TEXT NOT NULL,  -- Tên chi tiết dịch vụ
    Description TEXT,  -- Mô tả dịch vụ
    Price DECIMAL(10) NOT NULL,  -- Giá thành
    Image TEXT,  -- Hình ảnh minh họa
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE ExtraCategories (
    ExtraCategoryID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name TEXT NOT NULL UNIQUE, -- Tên nhóm (Ví dụ: "Special Materials", "Add-on Service")
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE Extras (
    ExtraID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ExtraCategoryID UUID NOT NULL REFERENCES ExtraCategories(ExtraCategoryID),
    Name TEXT NOT NULL,  -- Tên dịch vụ bổ sung
    Description TEXT,  -- Mô tả
    Price DECIMAL(10) NOT NULL,  -- Giá dịch vụ bổ sung
    Image TEXT,  -- Ảnh minh họa (nếu có)
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE ServiceExtraMapping (
    MappingID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ServiceID UUID NOT NULL REFERENCES ServiceDetails(ServiceID),
    ExtraID UUID NOT NULL REFERENCES Extras(ExtraID)
);

CREATE TABLE Orders (
    OrderID TEXT PRIMARY KEY,
    UserID UUID NOT NULL REFERENCES Users(UserId),
	PickupLabel TEXT,
	PickupName TEXT,
	PickupPhone TEXT,
    PickupAddressDetail TEXT,
	PickupDescription TEXT,
	PickupLatitude DECIMAL(9,6),
	PickupLongitude DECIMAL(9,6),
	DeliveryLabel TEXT,
	DeliveryName TEXT,
	DeliveryPhone TEXT,
	DeliveryAddressDetail TEXT,
	DeliveryDescription TEXT,
	DeliveryLatitude DECIMAL(9,6),
	DeliveryLongitude DECIMAL(9,6),
    PickupTime TIMESTAMP WITH TIME ZONE,
    DeliveryTime TIMESTAMP WITH TIME ZONE,
    ShippingFee DECIMAL(10),
	ShippingDiscount DECIMAL(10),
	ApplicableFee DECIMAL(10),
	OtherPrice DECIMAL(10),
    TotalPrice DECIMAL(10),
	Discount DECIMAL(10),
	CurrentStatus TEXT, --CHECK (CurrentStatus IN ('INCART', 'PENDING', 'PROCESSING', 'COMPLETED', 'CANCELLED')) DEFAULT 'INCART',
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE OrderItems (
    OrderItemID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderID TEXT NOT NULL REFERENCES Orders(OrderID),
    ServiceID UUID NOT NULL REFERENCES ServiceDetails(ServiceID),
    Quantity INT,--DEFAULT 1 CHECK (Quantity > 0),
    BasePrice DECIMAL(10), -- Lưu giá tại thời điểm đặt hàng
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE OrderExtras (
    OrderExtraID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderItemID UUID NOT NULL REFERENCES OrderItems(OrderItemID),
    ExtraID UUID NOT NULL REFERENCES Extras(ExtraID),
    ExtraPrice DECIMAL(10), -- Lưu giá tại thời điểm đặt hàng
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE OrderStatusHistory (
    StatusHistoryID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderID TEXT NOT NULL REFERENCES Orders(OrderID),
    Status TEXT, --CHECK (Status IN ('INCART', 'PENDING', 'PICKUP', 'PROCESSING', 'SHIPPING', 'DELIVERED', 'COMPLETED', 'CANCELLED')),
    StatusDescription TEXT,  -- Mô tả chi tiết trạng thái (VD: "Đơn hàng đã rời khỏi kho", "Shipper đang giao hàng")
	Notes TEXT,
   	UpdatedBy UUID REFERENCES Users(UserID), -- Ai đã thay đổi trạng thái này?
	CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE OrderPhotos (
    PhotoID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    StatusHistoryID UUID NOT NULL REFERENCES OrderStatusHistory(StatusHistoryID),
    PhotoUrl TEXT NOT NULL
);

CREATE TABLE OrderAssignmentHistory (
    AssignmentID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderID TEXT NOT NULL REFERENCES Orders(OrderID), 
    AssignedTo UUID NOT NULL REFERENCES Users(UserId), -- Ai nhận nhiệm vụ (Driver hoặc Staff)
    AssignedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    Status TEXT DEFAULT 'Assigned',-- CHECK (Status IN ('Assigned', 'Accepted', 'Declined', 'Completed')),
    DeclineReason TEXT, -- Lý do từ chối
    CompletedAt TIMESTAMP WITH TIME ZONE -- Thời điểm hoàn thành nhiệm vụ
);

CREATE TABLE PaymentMethods (
    PaymentMethodID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name TEXT NOT NULL UNIQUE, -- Tên phương thức thanh toán (VD: Tiền mặt, Chuyển khoản, Ví điện tử)
    Description TEXT,  -- Mô tả chi tiết (nếu cần)
    IsActive BOOLEAN DEFAULT TRUE,  -- Đánh dấu phương thức này có khả dụng hay không
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE Payments (
    PaymentID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderID TEXT NOT NULL REFERENCES Orders(OrderID),
    PaymentDate TIMESTAMP WITH TIME ZONE DEFAULT NOW(), -- Ngày thanh toán
    Amount DECIMAL(10) NOT NULL,-- Số tiền thanh toán
    PaymentMethodID UUID NOT NULL REFERENCES PaymentMethods(PaymentMethodID), -- Liên kết phương thức thanh toán
    PaymentStatus TEXT,--CHECK (PaymentStatus IN ('PENDING', 'PAID', 'FAILED', 'REFUNDED')) DEFAULT 'PENDING',
    TransactionID TEXT,      -- Mã giao dịch (đối với thanh toán online)
    PaymentMetadata JSONB,   -- Lưu trữ thêm dữ liệu phản hồi từ cổng thanh toán (token, mã xác thực, response JSON, v.v.)
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UpdatedAt TIMESTAMP WITH TIME ZONE
);

CREATE TABLE DriverLocationHistory (
    HistoryID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    DriverID UUID NOT NULL REFERENCES Users(UserID),
    OrderID TEXT REFERENCES Orders(OrderID),
    Latitudep DECIMAL(9,6) NOT NULL,
    Longitude DECIMAL(9,6) NOT NULL,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE Ratings (
    RatingID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserID UUID NOT NULL REFERENCES Users(UserID), -- Ai đã đánh giá
    ServiceID UUID NOT NULL REFERENCES ServiceDetails(ServiceID), -- Dịch vụ nào được đánh giá
    OrderID TEXT NOT NULL REFERENCES Orders(OrderID), -- Liên kết với đơn hàng đã sử dụng dịch vụ
    Rating INT,-- CHECK (Rating BETWEEN 1 AND 5) NOT NULL, -- Điểm đánh giá từ 1 đến 5
    Review TEXT, -- Nhận xét của người dùng
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW() -- Ngày đánh giá
);

CREATE TABLE DiscountCodes (
    DiscountCodeID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Code TEXT NOT NULL UNIQUE,  -- Mã giảm giá, ví dụ: "SALE20"
    Description TEXT,-- Mô tả mã giảm giá
    DiscountType TEXT,-- CHECK (DiscountType IN ('fixed', 'percentage')) NOT NULL,  -- Kiểu giảm giá: fixed hoặc percentage
    Value DECIMAL(10) NOT NULL,  -- Giá trị giảm: nếu fixed là số tiền, nếu percentage là phần trăm
    AppliesTo TEXT,-- CHECK (AppliesTo IN ('shipping', 'order', 'both')) NOT NULL,  -- Đối tượng áp dụng: phí ship, đơn hàng, hoặc cả hai
    MinimumOrderValue DECIMAL(10),  -- Giá trị đơn hàng tối thiểu để áp dụng mã (nếu có)
    MaximumDiscount DECIMAL(10),    -- Giá trị giảm tối đa (áp dụng với discount theo phần trăm)
    UsageLimit INT,                   -- Số lần sử dụng mã trên toàn hệ thống (nếu có)
    UsagePerUser INT,                 -- Số lần sử dụng tối đa cho mỗi user (nếu có)
    StartDate TIMESTAMP WITH TIME ZONE,  -- Thời điểm mã bắt đầu hiệu lực
    EndDate TIMESTAMP WITH TIME ZONE,    -- Thời điểm mã hết hiệu lực
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE OrderDiscounts (
    OrderDiscountID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderID TEXT NOT NULL REFERENCES Orders(OrderID),
    DiscountCodeID UUID NOT NULL REFERENCES DiscountCodes(DiscountCodeID),
    AppliedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW(),  -- Thời điểm áp mã
    DiscountAmount DECIMAL(10)  -- Số tiền giảm được áp dụng cho đơn hàng
);

CREATE TABLE DiscountCodeUsers (
    ID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    DiscountCodeID UUID NOT NULL REFERENCES DiscountCodes(DiscountCodeID),
    UserID UUID NOT NULL REFERENCES Users(UserId),
    AssignedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE RewardRedemptionOptions (
    OptionID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    DiscountAmount DECIMAL(10) NOT NULL,    -- Số tiền giảm: 30000, 50000, 100000,...
    RequiredPoints INT NOT NULL,              -- Số điểm cần đổi (30, 50, 100, ...)
    OptionDescription TEXT,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE RewardTransactions (
    RewardTransactionID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserID UUID NOT NULL REFERENCES Users(UserId),
    OrderID TEXT,  -- Liên kết đơn hàng nếu có
    TransactionType TEXT,-- NOT NULL CHECK (TransactionType IN ('earn', 'redeem')),
    Points INT NOT NULL,  -- Số điểm thay đổi: dương với 'earn', âm với 'redeem'
    OptionID UUID REFERENCES RewardRedemptionOptions(OptionID),  -- Áp dụng nếu là giao dịch đổi điểm
    TransactionDate TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    Note TEXT
);

--INSERT DATA