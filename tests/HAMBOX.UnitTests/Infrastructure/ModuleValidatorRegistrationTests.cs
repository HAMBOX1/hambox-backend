using FluentValidation;
using HAMBOX.Modules.Legal.Application.Features.Legal;
using HAMBOX.Modules.Legal.Infrastructure.Extensions;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// Validators that are never registered silently never run, so a module can ship without any input
/// validation. Legal and Suppliers were missing their registration, and FluentValidation skips
/// <c>internal</c> validators unless asked to include them (Identity, Commerce, Legal).
/// </summary>
public sealed class ModuleValidatorRegistrationTests
{
    private static ServiceCollection Register(Action<IServiceCollection, IConfiguration> add)
    {
        var services = new ServiceCollection();
        add(services, new ConfigurationBuilder().Build());
        return services;
    }

    [Fact]
    public void Legal_RegistersItsValidators() =>
        Assert.Contains(
            Register((s, c) => s.AddLegalInfrastructure(c)),
            d => d.ServiceType == typeof(IValidator<CreateLegalSectionCommand>));

    [Fact]
    public void Suppliers_RegistersItsValidators() =>
        Assert.Contains(
            Register((s, c) => s.AddSuppliersInfrastructure(c)),
            d => d.ServiceType == typeof(IValidator<CreateSupplierCommand>));
}
