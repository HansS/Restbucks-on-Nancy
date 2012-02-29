namespace RestBucks.Tests.Resources
{
  using System;
  using System.Linq;

  using Moq;

  using NUnit.Framework;

  using Nancy;
  using Nancy.Testing;

  using RestBucks.Data;
  using RestBucks.Domain;
  using Infrastructure.Linking;
  using RestBucks.Resources.Orders;
  using RestBucks.Resources.Orders.Representations;
  using Util;

  using SharpTestsEx;

  [TestFixture]
  public class WhenUserCreatesAnOrder
  {
    private readonly Product latte = new Product
                                     {
                                       Name = "latte",
                                       Price = 2.5m,
                                       Customizations =
                                         {
                                           new Customization
                                           {
                                             Name = "size",
                                             PossibleValues = {"small", "medium"}
                                           }
                                         }
                                     };

    private readonly IResourceLinker resourceLinker = new ResourceLinker();

    public Browser CreateAppProxy(
      IRepository<Order> orderRepository = null,
      IRepository<Product> productRepository = null)
    {
      var defaultProductRepository = new RepositoryStub<Product>(
        latte, new Product {Name = "Other", Price = 3.6m});

      return new Browser(
        new ConfigurableBootstrapper
          (with =>
           {
             with.Dependency<IRepository<Product>>(productRepository ?? defaultProductRepository);
             with.Dependency<IRepository<Order>>(orderRepository ?? new RepositoryStub<Order>());
           }
          ));
    }

    [Test]
    public void WhenAProductDoesNotExist_ThenReturn400AndTheProperREasonPhrase()
    {
      var appProxy = CreateAppProxy();
      var orderRepresentation = new OrderRepresentation
                                {
                                  Items = {new OrderItemRepresentation {Name = "beer"}}
                                };

      var result = 
        appProxy.Post("/orders/", with =>
          with.Body(orderRepresentation.ToXmlString()));


      result.StatusCode.Should().Be.EqualTo(HttpStatusCode.BadRequest);
      result.Headers["ReasonPhrase"].Should().Be.EqualTo("We don't offer beer");
    }

    [Test]
    public void WhenItemHasQuantity0_ThenReturn400AndTheProperREasonPhrase()
    {
      var appProxy = CreateAppProxy();
      var orderRepresentation = new OrderRepresentation
                                {
                                  Items = {new OrderItemRepresentation {Name = "latte", Quantity = 0}}
                                };
      
      // act
      var result =
        appProxy.Post("/orders/", with =>
          with.Body(orderRepresentation.ToXmlString()));

      // assert
      result.StatusCode.Should().Be.EqualTo(HttpStatusCode.BadRequest);
      result.Headers["ReasonPhrase"].Should().Be.EqualTo("Item 0: Quantity should be greater than 0.");
    }

    [Test]
    public void WhenOrderIsOk_ThenInsertANewOrderWithTheProductsAndPrice()
    {
      var orderRepository = new RepositoryStub<Order>();
      var appProxy = CreateAppProxy(orderRepository);
      var orderRepresentation = new OrderRepresentation
                                {Items = {new OrderItemRepresentation {Name = "latte", Quantity = 1}}};

      //act
      appProxy.Post("/orders/", with =>
                    with.Body(orderRepresentation.ToXmlString()));

      // assert
      var order = orderRepository.RetrieveAll().First();
      order.Satisfy(_ => _.Items.Any(i => i.Product == latte && i.UnitPrice == 2.5m && i.Quantity == 1));
    }

    [Test]
    public void WhenOrderIsOk_ThenInsertANewOrderWithTheDateTime()
    {
      var orderRepository = new RepositoryStub<Order>();
      var appProxy = CreateAppProxy(orderRepository);
      var orderRepresentation = new OrderRepresentation
                                {Items = {new OrderItemRepresentation {Name = "latte", Quantity = 1}}};

      //act
      var result = appProxy.Post("/orders/", with =>
                                 with.Body(orderRepresentation.ToXmlString()));

      var order = orderRepository.RetrieveAll().First();
      order.Date.Should().Be.EqualTo(DateTime.Today);
    }

    [Test]
    public void WhenOrderIsOk_ThenInsertANewOrderWithTheLocationInfo()
    {
      var orderRepository = new RepositoryStub<Order>();
      var appProxy = CreateAppProxy(orderRepository);
      var orderRepresentation = new OrderRepresentation
                                {
                                  Location = Location.InShop,
                                  Items = {new OrderItemRepresentation {Name = "latte", Quantity = 1}}
                                };

      //act
      var result = appProxy.Post("/orders/", with =>
                                 with.Body(orderRepresentation.ToXmlString()));

      var order = orderRepository.RetrieveAll().First();
      order.Location.Should().Be.EqualTo(Location.InShop);
    }

    [Test]
    public void WhenOrderIsOk_ThenResponseHasStatus201AndLocation()
    {
      var orderRepository = new Mock<IRepository<Order>>();
      orderRepository.Setup(or => or.MakePersistent(It.IsAny<Order[]>()))
        .Callback<Order[]>(o => o.First().Id = 123);

      var expectedUriToTheNewOrder =
        resourceLinker.GetUri<OrderResourceHandler>(or => or.Get(0, null), new { orderId = "123" });

      var appProxy = CreateAppProxy(orderRepository.Object);
      var orderRepresentation = new OrderRepresentation
                                {
                                  Items = {new OrderItemRepresentation {Name = "latte", Quantity = 1}}
                                };
      // act
      var result = appProxy.Post("/orders/", with => 
                                 with.Body(orderRepresentation.ToXmlString()));


      result.StatusCode.Should().Be.EqualTo(HttpStatusCode.Created);
      result.Headers["Location"].Should().Be.EqualTo(expectedUriToTheNewOrder);
    }
  }
}