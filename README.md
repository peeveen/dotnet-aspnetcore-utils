# Peeveen.AspNetCore.Utils

Some handy extensions and classes for ASP.NET Core that I like to use.

## BindWithDynamics / BindDynamics

Extension methods that will attempt to build populated `ExpandoObject`s to fulfil any
`dynamic` members in your options type (normally, any `dynamic` members are just
bound to an empty instance of `Object`, or null).

### Usage

```csharp
serviceCollection
	.AddOptions<MyOptions>()
	.BindWithDynamics(configurationSection);
```

This is equivalent to the following code:

```csharp
serviceCollection
	.AddOptions<MyOptions>()
	.Bind(configurationSection)
	.Configure(options => options.BindDynamics(configurationSection));
```
