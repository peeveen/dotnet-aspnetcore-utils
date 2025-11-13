using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Peeveen.AspNetCore.Utils.Test;

public class ConfigurationBuilderTests {
	[Fact]
	public void TestConfigurationSources() {
		var configuration = new ConfigurationBuilder()
			// Test will execute in bin/Debug/net9.0 or similar, so go back three levels to reach the project folder root
			.SetBasePath(Path.Join(Directory.GetCurrentDirectory(), "..", "..", ".."))
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Key1", "ValueFromInMemory" },
				{ "Key2", "ValueFromInMemory" },
			})
			.AddJsonFile("appsettings.json", optional: true)
			.AddYamlFile("appsettings", "yaml", "Development")
			.AddYamlFile("appsettings", "yaml")
			.Build();
		var key1Value = configuration["Key1"];
		var key2Value = configuration["Key2"];
		var key3Value = configuration["Key3"];
		var key4Value = configuration["Key4"];
		var key5Value = configuration["Key5"];

		key1Value.Should().Be("ValueFromInMemory"); // From In-Memory
		key2Value.Should().Be("ValueFromJson");     // Overridden by JSON
		key3Value.Should().Be("ValueFromYamlDev");  // From appsettings.Development.yaml
		key4Value.Should().Be("ValueFromYaml");     // From appsettings.yaml
		key5Value.Should().Be("ValueFromYaml");     // From appsettings.yaml
	}
}
