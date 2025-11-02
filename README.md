# Peeveen.AspNetCore.Utils

Some handy extensions and classes for ASP.NET Core that I like to use.

## BindDynamics

Extension method for `IConfiguration` that will attempt to build populated `ExpandoObject`s to
fulfil any `dynamic` members in your options type (normally, any `dynamic` members are just
bound to an empty instance of `Object`, or null).

### Usage

```csharp
serviceCollection.AddOptions<MyOptions>()
	.Bind(configurationSection)
	.Configure(options => options.BindDynamics(section));
```