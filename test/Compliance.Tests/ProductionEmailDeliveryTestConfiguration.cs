using Microsoft.AspNetCore.Hosting;

namespace Bdgrz.Compliance.Tests;

static class ProductionEmailDeliveryTestConfiguration
{
    public static void Apply(IWebHostBuilder builder)
    {
        builder.UseSetting("Compliance:EmailDelivery:Mode", "smtp");
        builder.UseSetting("Compliance:EmailDelivery:Smtp:Host", "smtp.example.com");
        builder.UseSetting("Compliance:EmailDelivery:Smtp:Port", "587");
        builder.UseSetting("Compliance:EmailDelivery:Smtp:FromAddress", "verify@example.com");
        builder.UseSetting("Compliance:EmailDelivery:ActiveTokenKeyId", "test-key");
        builder.UseSetting("Compliance:EmailDelivery:TokenKeys:test-key",
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
    }
}
