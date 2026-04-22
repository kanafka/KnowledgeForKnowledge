using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Tests.Helpers;

namespace Tests.InfraServices;

public class JwtServiceTests
{
    private static JwtService CreateService(int expiryDays = 7)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]        = "super_secret_key_for_unit_tests_32chars!!",
                ["Jwt:Issuer"]     = "TestIssuer",
                ["Jwt:Audience"]   = "TestAudience",
                ["Jwt:ExpiryDays"] = expiryDays.ToString()
            })
            .Build();
        return new JwtService(config);
    }

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var svc = CreateService();
        var account = Fakes.Account();
        var token = svc.GenerateToken(account);
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_ContainsAccountIdClaim()
    {
        var svc = CreateService();
        var account = Fakes.Account();
        var token = svc.GenerateToken(account);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var sub = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
               ?? jwt.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
        sub.Should().Be(account.AccountID.ToString());
    }

    [Fact]
    public void GenerateToken_ContainsLoginClaim()
    {
        var svc = CreateService();
        var account = Fakes.Account(login: "test@example.com");
        var token = svc.GenerateToken(account);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var name = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
        name.Should().Be("test@example.com");
    }

    [Fact]
    public void GenerateToken_AdminAccount_ContainsAdminRole()
    {
        var svc = CreateService();
        var account = Fakes.Account(isAdmin: true);
        var token = svc.GenerateToken(account);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
        role.Should().Be("Admin");
    }

    [Fact]
    public void GenerateToken_NonAdminAccount_ContainsUserRole()
    {
        var svc = CreateService();
        var account = Fakes.Account(isAdmin: false);
        var token = svc.GenerateToken(account);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
        role.Should().Be("User");
    }

    [Fact]
    public void GenerateToken_ExpiresAfterConfiguredDays()
    {
        var svc = CreateService(expiryDays: 3);
        var account = Fakes.Account();
        var before = DateTime.UtcNow;
        var token = svc.GenerateToken(account);
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.ValidTo.Should().BeAfter(before.AddDays(2).AddHours(23));
        jwt.ValidTo.Should().BeBefore(after.AddDays(3).AddMinutes(1));
    }

    [Fact]
    public void GenerateToken_HasCorrectIssuerAndAudience()
    {
        var svc = CreateService();
        var account = Fakes.Account();
        var token = svc.GenerateToken(account);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("TestIssuer");
        jwt.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public void GenerateToken_TwoCallsSameAccount_ReturnsDifferentTokens()
    {
        var svc = CreateService();
        var account = Fakes.Account();
        var t1 = svc.GenerateToken(account);
        // small delay to ensure different iat
        System.Threading.Thread.Sleep(1100);
        var t2 = svc.GenerateToken(account);
        t1.Should().NotBe(t2);
    }
}
