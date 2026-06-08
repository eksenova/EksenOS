using System.ComponentModel.DataAnnotations;
using Eksen.Emailing.Gmail;
using Eksen.TestBase;
using Eksen.ValueObjects;
using Eksen.ValueObjects.Emailing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.Emailing.Gmail.Tests;

public class GmailConfigurationTests : EksenUnitTestBase
{
    private static List<ValidationResult> Validate(GmailConfiguration configuration)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(configuration);
        Validator.TryValidateObject(configuration, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Should_Have_Sensible_Defaults()
    {
        // Arrange & Act
        var configuration = new GmailConfiguration
        {
            DefaultFromAddress = EmailAddress.Create("default@example.com"),
            DefaultFromName = "Default Sender"
        };

        // Assert
        configuration.ServiceAccountFile.ShouldBe(GmailConfiguration.DefaultServiceAccountFile);
        configuration.ApplicationName.ShouldBe(GmailConfiguration.DefaultApplicationName);
        configuration.ImpersonatedUser.ShouldBeNull();
    }

    [Fact]
    public void Validation_Should_Pass_For_Valid_Configuration()
    {
        // Arrange
        var configuration = new GmailConfiguration
        {
            DefaultFromAddress = EmailAddress.Create("default@example.com"),
            DefaultFromName = "Default Sender",
            ServiceAccountFile = "service-account.json"
        };

        // Act
        var results = Validate(configuration);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Validation_Should_Fail_When_DefaultFromName_Is_Empty()
    {
        // Arrange
        var configuration = new GmailConfiguration
        {
            DefaultFromAddress = EmailAddress.Create("default@example.com"),
            DefaultFromName = string.Empty
        };

        // Act
        var results = Validate(configuration);

        // Assert
        results.ShouldContain(r => r.MemberNames.Contains(nameof(GmailConfiguration.DefaultFromName)));
    }

    [Fact]
    public void Validation_Should_Fail_When_ServiceAccountFile_Is_Empty()
    {
        // Arrange
        var configuration = new GmailConfiguration
        {
            DefaultFromAddress = EmailAddress.Create("default@example.com"),
            DefaultFromName = "Default Sender",
            ServiceAccountFile = string.Empty
        };

        // Act
        var results = Validate(configuration);

        // Assert
        results.ShouldContain(r => r.MemberNames.Contains(nameof(GmailConfiguration.ServiceAccountFile)));
    }

    [Fact]
    public void Options_Binding_Should_Map_Configuration_Section()
    {
        // Arrange
        // Register the value-object TypeConverter so configuration binding can map the
        // EmailAddress fields from their string representation.
        new EksenValueObjectOptions().Add<EmailAddress>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleCloud:Gmail:DefaultFromAddress"] = "noreply@example.com",
                ["GoogleCloud:Gmail:DefaultFromName"] = "Eksen Notifications",
                ["GoogleCloud:Gmail:ServiceAccountFile"] = "keys/sa.json",
                ["GoogleCloud:Gmail:ImpersonatedUser"] = "impersonated@example.com",
                ["GoogleCloud:Gmail:ApplicationName"] = "MyApp"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services
            .AddOptions<GmailConfiguration>()
            .BindConfiguration(GmailConfiguration.DefaultConfigSectionPath)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();

        // Act
        var bound = provider.GetRequiredService<IOptions<GmailConfiguration>>().Value;

        // Assert
        bound.DefaultFromAddress.Value.ShouldBe("noreply@example.com");
        bound.DefaultFromName.ShouldBe("Eksen Notifications");
        bound.ServiceAccountFile.ShouldBe("keys/sa.json");
        bound.ImpersonatedUser.ShouldNotBeNull();
        bound.ImpersonatedUser.Value.ShouldBe("impersonated@example.com");
        bound.ApplicationName.ShouldBe("MyApp");
    }
}
