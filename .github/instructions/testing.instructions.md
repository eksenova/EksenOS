---
applyTo: "**/*.Tests/**"
---

# Testing Instructions

## General

- Use **xUnit** as the test framework.
- Use **Shouldly** for all assertions.
- Define builders for each entity to be tested (using the Bogus library and Faker class)
  - Accept optional parameters using Fluent syntax
  - Accept required parameters in Build() method
- Follow the **AAA pattern** (Arrange – Act – Assert) and mark each section with a comment.
- Name tests descriptively using the format: `MethodName_Should_ExpectedBehavior_When_Condition`.
  - `CreateOrder_Should_Throw_Duplicate_Order_Error_When_Order_Number_Already_Exists`
  - `CreateOrder_Should_Be_Successful_With_Multiple_Order_Items`

Note: The concrete entities shown in the examples here represent random isolated example implementations that are independent of any specific product or codebase.

## Test Types

### Unit Tests

- Inherit from `EksenUnitTestBase`.
- Use **Moq** for dependent services.
- Test exactly **one method / one scenario** per test.
- Only set up mock methods that are **actually invoked** by the code under test.
- Verify that mocked methods were called the **expected number of times**.
- Create a test for **every branch** of the code under test—not just happy paths. Assume all kinds of inputs and errors are possible.
- Use for code whose correctness can be validated through **logical / theoretical proofs** (business rules, calculations, mapping, validation, etc.).
- Always define **fake implementations** of external services.

#### Unit Test Example – Happy Path

```csharp
public class OrderAppServiceUnitTests : MyAppUnitTestBase
{
    [Fact]
    public async Task CreateOrder_Should_Be_Successful_With_Multiple_Order_Items()
    {
        // Arrange
        var orderNumber = OrderNumber.Create("ORD-001");

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.FindByOrderNumberAsync(
                orderNumber,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync<Order?>(null);
        orderRepository
            .Setup(r => r.InsertAsync(
                It.IsAny<Order>(), 
                It.IsAny<bool>(), 
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var product = new ProductBuilder()
            .Build();
        var productId = product.Id;

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByIdAsync(
                productId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var appService = new OrderAppService(
            orderRepository.Object,
            productRepository.Object,
            ...
        );

        var input = new CreateOrderInput
        {
            OrderNumber = orderNumber.Value,
            Items =
            [
                new CreateOrderItemInput
                { 
                    ProductId = productId.Value,
                    Quantity = 1,
                    ...
                },
                ...
            ]
        };

        // Act
        var createdOrderDto = await appService.CreateAsync(input);

        // Assert
        createdOrderDto.ShouldNotBeNull();
        createdOrderDto.Items.Count.ShouldBe(...);
        createdOrderDto.Items[0].Quantity.ShouldBe(1);
        createdOrderDto.Items[0].ProductId.ShouldBe(productId.Value);
        ...


        orderRepository.Verify(
            r => r.FindByOrderNumberAsync(orderNumber, It.IsAny<CancellationToken>()),
            Times.Once);

        productRepository.Verify(
            r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()),
            Times.Once);

        orderRepository.Verify(
            r => r.InsertAsync(It.IsAny<Order>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}


public class OrderNumberUnitTests : MyAppUnitTestBase
{
    [Theory]
    [InlineData(...)]
    [InlineData(...)]
    public void Create_Should_Be_Successful(string orderNumber)
    {
        // Arrange & Act & Assert
        Should.NotThrow(() => OrderNumber.Create(orderNumber));
    }

    ...
}
```

#### Unit Test Example – Bad Path

```csharp
public class OrderAppServiceUnitTests : MyAppUnitTestBase
{
    [Fact]
    public async Task CreateOrder_Should_Throw_Duplicate_Order_Error_When_Order_Number_Already_Exists()
    {
        // Arrange
        var product = new ProductBuilder()
            .Build();
        var productId = product.Id;

        var existingOrder = new OrderBuilder()
            .AddRandomItems()
            .AddItemWithProductId(productId)
            .Build();

        var existingOrderNumber = existingOrder.OrderNumber;

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.FindByOrderNumberAsync(
                existingOrderNumber,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync<Order?>(existingOrder);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetByIdAsync(
                productId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var appService = new OrderAppService(
            orderRepository.Object,
            productRepository.Object,
            ...
        );

        var input = new CreateOrderInput
        {
            OrderNumber = existingOrderNumber.Value,
            Items =
            [
                new CreateOrderItemInput
                { 
                    ProductId = productId.Value,
                    Quantity = 1,
                    ...
                },
                ...
            ]
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<EksenException>(
            () => appService.CreateAsync(input, CancellationToken.None));

        exception.Descriptor.ShouldBe(OrderErrors.DuplicateOrderNumber);
        exception.Data.ShouldContainKeyAndValue("orderNumber", existingOrderNumber);

        orderRepository.Verify(
            r => r.InsertAsync(It.IsAny<Order>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);

        orderRepository.Verify(
            r => r.FindByOrderNumberAsync(
                existingOrderNumber,
                It.IsAny<CancellationToken>()),
            Times.Once);

        productRepository.Verify(
            r => r.GetByIdAsync(
                productId,
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrder_Should_Throw_When_Items_Are_Empty()
    {
        // Arrange
        var orderRepository = new Mock<IOrderRepository>();
        var productRepository = new Mock<IProductRepository>();

        var appService = new OrderAppService(
            orderRepository.Object,
            productRepository.Object,
            ...
        );

        var input = new CreateOrderInput
        {
            OrderNumber = "ORD-001",
            Items = []
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<EksenException>(
            () => appService.CreateAsync(input, CancellationToken.None));

        exception.Descriptor.ShouldBe(OrderErrors.EmptyOrderItems);
    }

    ...
}

public class OrderNumberUnitTests : MyAppUnitTestBase
{
    [Fact]
    public void Create_Should_Throw_When_Order_Number_Is_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<EksenException>(() => OrderNumber.Create(""));
    }

    ...
}
```

### Integration Tests

- Inherit from a database test base (e.g. `EksenSqlServerTestBase` for SQL Server).
- **Do not** use Moq.
- **Do not** test presentation-related concerns (controllers, middleware, etc.).
- Only use for code that **interacts with a database**.
- Only test **happy paths** — edge cases should already be covered by unit tests — unless there is a **database-specific bad path** (e.g. raw SQL queries that must not allow SQL injection, unique-index conflicts for concurrent operations, etc.).
- **Do not** test database engine behaviour itself. Assume the database correctly enforces constraints (unique indexes, foreign keys, etc.).
- Override `ConfigureServices` or `ConfigureEksen` to register application-specific services.

#### Integration Test Example

```csharp
public class OrderAppServiceIntegrationTests(SqlServerFixture fixture) : MyAppSqlServerTestBase(fixture)
{
    // (Assume OrderAppService uses SQL Server repositories in this example)

    [Fact]
    public async Task Create_Should_Be_Successful()
    {
        // Arrange
        var productRepository = GetRequiredService<IProductRepository>();
        var appService = GetRequiredService<IOrderAppService>();

        var product1 = new ProductBuilder()
            .Build();

        var product2 = new ProductBuilder()
            .Build();

        await productRepository.InsertManyAsync([product1, product2], true);

        var orderNumber = OrderNumber.Create("ORD-001");

        var createOrderInput = new CreateOrderInput
        {
            OrderNumber = orderNumber.Value,
            Items =
            [
                new CreateOrderItemInput
                {
                    ProductId = product1.Id.Value,
                    Quantity = 1,
                    ...
                },
                new CreateOrderItemInput
                {
                    ProductId = product2.Id.Value,
                    Quantity = 3,
                    ...
                }
            ]
        };

        // Act
        var createdOrderDto = await appService.CreateAsync(createOrderInput, autoSave: true);

        // Assert
        var orderRepository = GetRequiredService<IOrderRepository>();
        var fetched = await orderRepository.FindAsync(OrderId.Create(createdOrderDto.Id));
        fetched.ShouldNotBeNull();
        fetched.OrderNumber.ShouldBe(orderNumber);
        fetched.Items.Count.ShouldBe(2);
        fetched.Items[0].ProductId.ShouldBe(product1.Id);
        fetched.Items[0].Quantity.ShouldBe(1);
        ...
        fetched.Items[1].ProductId.ShouldBe(product2.Id);
        fetched.Items[1].Quantity.ShouldBe(3);
        ...
    }

    // ...
}
```

### End-to-End Tests

- Inherit from `EksenWebTestBase<TProgram, TDbContext>`.
- **Do not** use Moq.
- Tests involve the **presentation layer** (ASP.NET Core HTTP layer).
- Cover scenarios such as authentication, authorization, controller endpoints, etc.
- Like integration tests, only test **happy paths** unless the bad path can **only occur at the layer level** (e.g. unauthorized access, missing auth tokens, content negotiation).
- **Do not** test framework behaviour itself. Assume controllers are mapped, model binding works, etc.

#### End-to-End Test Example – Happy Path

```csharp
public class OrderEndpointTests(SqlServerFixture fixture)
    : MyAppWebTestBase(fixture)
{
    [Fact]
    public async Task CreateOrder_Endpoint_Should_Return_Created()
    {
        // Arrange
        var customerRole = new RoleBuilder()
            .Build(name: "Customer");

        var roleRepository = GetRequiredService<IRoleRepository>();
        await roleRepository.InsertAsync(customerRole, true);

        await GrantPermissionsAsync(customerRole, AppPermissions.Orders.Create);

        var userName = "test-customer";
        var password = "testPass$12312!!";

        var customer = new UserBuilder()
            .WithUserName(userName)
            .WithPassword(password)
            .Build(customerRole);

        var userRepository = GetRequiredService<IUserRepository>();
        await userRepository.InsertAsync(customer, true);

        await AuthenticateAsync(userName, password);

        var productRepository = GetRequiredService<IProductRepository>();

        var product1 = new ProductBuilder()
            .Build();

        var product2 = new ProductBuilder()
            .Build();

        await productRepository.InsertManyAsync([product1, product2], true);

        var orderNumber = OrderNumber.Create("ORD-001");

        var input = new CreateOrderInput
        {
            OrderNumber = orderNumber.Value,
            Items =
            [
                new CreateOrderItemInput
                {
                    ProductId = product1.Id.Value,
                    Quantity = 1,
                    ...
                },
                new CreateOrderItemInput
                {
                    ProductId = product2.Id.Value,
                    Quantity = 3,
                    ...
                }
            ]
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", input);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var orderDto = await response.Content.ReadFromJsonAsync<DetailedOrderDto>();
        orderDto.ShouldNotBeNull();
        orderDto.OrderNumber.ShouldBe(orderNumber.Value);
        orderDto.Items.Count.ShouldBe(2);
        orderDto.Items[0].ProductId.ShouldBe(product1.Id.Value);
        orderDto.Items[0].Quantity.ShouldBe(1);
        ...
        orderDto.Items[1].ProductId.ShouldBe(product2.Id.Value);
        orderDto.Items[1].Quantity.ShouldBe(3);
        ...
    }
}
```

#### End-to-End Test Example – Layer-Specific Bad Path

```csharp
public class OrderEndpointTests(SqlServerFixture fixture)
    : MyAppWebTestBase<Program, AppDbContext>
{
    [Fact]
    public async Task CreateOrder_Endpoint_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange
        var input = new CreateOrderInput
        {
            OrderNumber = "ORD-001",
            Items = [ ... ]
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", input);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var responseDto = await response.Content.ReadFromJsonAsync<ErrorResposeDto>();
        responseDto.Code.ShouldBe(CommonErrors.Unauthorized.Code);
    }

    [Fact]
    public async Task CreateOrder_Endpoint_Should_Return_Forbidden_Without_Permission()
    {
        // Arrange
        var customerRole = new RoleBuilder()
            .Build(name: "Customer");

        var roleRepository = GetRequiredService<IRoleRepository>();
        await roleRepository.InsertAsync(customerRole, true);

        var userName = "test-customer";
        var password = "testPass$12312!!";

        var customer = new UserBuilder()
            .WithUserName(userName)
            .WithPassword(password)
            .Build(customerRole);

        var userRepository = GetRequiredService<IUserRepository>();
        await userRepository.InsertAsync(customer, true);

        await AuthenticateAsync(userName, password);

        var input = new CreateOrderInput
        {
            OrderNumber = "ORD-001",
            Items = [new CreateOrderItemInput { ProductId = Guid.NewGuid(), Quantity = 1 }]
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", input);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var responseDto = await response.Content.ReadFromJsonAsync<ErrorResposeDto>();
        responseDto.Code.ShouldBe(CommonErrors.Forbidden.Code);
    }
}
```

## Test Base Classes

| Base Class | Package | Purpose |
|---|---|---|
| `EksenUnitTestBase` | `Eksen.TestBase` | Unit tests without DI |
| `EksenServiceTestBase` | `Eksen.TestBase` | Tests with DI / service provider |
| `EksenDatabaseTestBase<TDbContext>` | `Eksen.TestBase` | Testcontainer-backed database tests |
| `EksenSqlServerTestBase` | Per test project | SQL Server–specific database base |
| `EksenWebTestBase<TProgram, TDbContext>` | `Eksen.TestBase.AspNetCore` | ASP.NET Core end-to-end tests |

### Overriding Service Registration

Override `ConfigureServices` for additional DI registrations (async-capable):

```csharp
protected override Task ConfigureServices(ServiceCollection services)
{
    services.AddFakeRandomStringGenerator(); // from Eksen.TestBase fake extensions
    return base.ConfigureServices(services);
}
```

Override `ConfigureEksen` for Eksen-specific builder configuration (sync):

```csharp
protected override void ConfigureEksen(IEksenBuilder builder)
{
    base.ConfigureEksen(builder);
    builder.AddAuditing();
}
```

### SQL Server Parallel Execution

SQL Server integration tests use a pool of Testcontainers governed by `EKSEN_SQL_MAX_WORKERS` (default: 2). Tests within the same `[Collection("SqlServer")]` share the pool. Each test acquires a clean worker, runs against its own empty database, and releases the worker on completion — enabling full xUnit parallel execution across collections.
