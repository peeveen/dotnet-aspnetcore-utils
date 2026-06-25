using System.Dynamic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Peeveen.AspNetCore.Utils.Test;

public class MyOptions {
	public required ExpandoObject Configuration { get; init; }
	public ExpandoObject? NotSetConfiguration { get; init; }
}

public class ConfigurationBindingTests {
	[Fact]
	public void TestBinding() {
		var stringVal = "9234u892384StringValue";
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "MyOptions:Configuration:StringVal", stringVal },
				{ "MyOptions:Configuration:DoubleVal", $"{double.MaxValue}" },
				{ "MyOptions:Configuration:IntVal", $"{int.MaxValue}" },
				{ "MyOptions:Configuration:LongVal", $"{long.MaxValue}" },
				{ "MyOptions:Configuration:BoolVal", "FalSe" }, // case insensitive
				{ "MyOptions:Configuration:OtherVal:Thing", "9876.543" },
				{ "MyOptions:Configuration:ArrayVal:0", "FirstArrayItem" },
				{ "MyOptions:Configuration:ArrayVal:1", "SecondArrayItem" },
				{ "MyOptions:Configuration:ArrayVal:2", "ThirdArrayItem" },
				{ "MyOptions:Configuration:ObjectVal:StringVal", new string([.. stringVal.Reverse()])},
				{ "MyOptions:Configuration:ObjectVal:DoubleVal", $"{double.MinValue}" },
				{ "MyOptions:Configuration:ObjectVal:IntVal", $"{int.MinValue}" },
				{ "MyOptions:Configuration:ObjectVal:LongVal", $"{long.MinValue}" },
				{ "MyOptions:Configuration:ObjectVal:BoolVal", "tRUe" }, // case insensitive
			})
			.Build();

		var services = new ServiceCollection();
		var section = configuration.GetSection("MyOptions");
		var nonExistentSection = configuration.GetSection("DoesNotExist");
		var blah = nonExistentSection.AsEnumerable().Any();
		services.AddOptions<MyOptions>()
			.BindWithDynamics(section)
			// Ensure that binding to a non-existent section does not eliminate all previous values
			.BindWithDynamics(nonExistentSection);

		var serviceProvider = services.BuildServiceProvider();
		var myOptions = serviceProvider.GetRequiredService<IOptions<MyOptions>>().Value;

		myOptions.Should().NotBeNull();
		((myOptions.Configuration as dynamic).StringVal as string).Should().Be(stringVal);
		((myOptions.Configuration as dynamic).DoubleVal as double?).Should().Be(double.MaxValue);
		((myOptions.Configuration as dynamic).IntVal as int?).Should().Be(int.MaxValue);
		((myOptions.Configuration as dynamic).LongVal as long?).Should().Be(long.MaxValue);
		((myOptions.Configuration as dynamic).BoolVal as bool?).Should().BeFalse();
		((myOptions.Configuration as dynamic).OtherVal.Thing as double?).Should().Be(9876.543);
		((myOptions.Configuration as dynamic).ObjectVal.StringVal as string).Should().Be(new string([.. stringVal.Reverse()]));
		((myOptions.Configuration as dynamic).ObjectVal.DoubleVal as double?).Should().Be(double.MinValue);
		((myOptions.Configuration as dynamic).ObjectVal.IntVal as int?).Should().Be(int.MinValue);
		((myOptions.Configuration as dynamic).ObjectVal.LongVal as long?).Should().Be(long.MinValue);
		((myOptions.Configuration as dynamic).ObjectVal.BoolVal as bool?).Should().BeTrue();
		((myOptions.Configuration as dynamic).ArrayVal[0] as string).Should().Be("FirstArrayItem");
		((myOptions.Configuration as dynamic).ArrayVal[1] as string).Should().Be("SecondArrayItem");
		((myOptions.Configuration as dynamic).ArrayVal[2] as string).Should().Be("ThirdArrayItem");
		myOptions.NotSetConfiguration.Should().BeNull();
	}
}