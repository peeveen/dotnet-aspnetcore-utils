using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("Peeveen.AspNetCore.Utils.Test")]

namespace Peeveen.AspNetCore.Utils;

/// <summary>
/// Extensions for retrieving options from an IConfiguration.
/// </summary>
public static class ConfigurationOptionsExtensions {
	private static readonly string DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

	/// <summary>
	/// Gets the data from the given section.
	/// </summary>
	/// <typeparam name="TOptions">Type of object to return.</typeparam>
	/// <param name="configuration">The configuration.</param>
	/// <param name="sectionName">Section from which to retrieve data to bind to the object.</param>
	/// <returns>Object containing configuration data.</returns>
	public static TOptions GetOptions<TOptions>(this IConfiguration configuration, string sectionName)
		where TOptions : class, new() {
		var instance = new TOptions();
		var configurationSection = configuration.GetSection(sectionName);
		configurationSection.Bind(instance);
		instance.BindDynamics(configurationSection);
		return instance;
	}

	private static readonly Type[] ExcludedPropertyTypes = [
		typeof(string),
		typeof(DateTime),
		typeof(Uri),
		typeof(Guid),
	];

	private static void CoerceType(dynamic? item, Action<dynamic> addFn) {
		// Apply type conversion logic here
		if (item is string strValue) {
			var containsDecimalPoint = strValue.Contains(DecimalSeparator);
			// Even if the string is e.g. 3255.0, we would want to treat that as a double,
			// regardless of whether it can be represented as an integer or not.
			if (!containsDecimalPoint && int.TryParse(strValue, out var intValue))
				addFn(intValue);
			else if (!containsDecimalPoint && long.TryParse(strValue, out var longValue))
				addFn(longValue);
			// Ignore floats, not much scope for that.
			else if (double.TryParse(strValue, out var doubleValue))
				addFn(doubleValue);
			else if (bool.TryParse(strValue, out var boolValue))
				addFn(boolValue);
			else
				addFn(strValue); // Keep as string if no conversion is possible
		} else
			addFn(item); // Keep original value if not a string
	}

	private static dynamic CoerceTypes(IList<dynamic> list) {
		// Binding from an IConfiguration to a List is all very well and good,
		// but IConfiguration is designed to only contain string data, so we end up with
		// an List full of strings.
		// Here, we try to coerce the data into suitable types.
		IList<dynamic> newList = [];
		foreach (var item in list)
			CoerceType(item, (Action<dynamic>)newList.Add);
		return newList;
	}

	private static dynamic CoerceTypes(ExpandoObject data) {
		// Binding from an IConfiguration to an ExpandoObject is all very well and good,
		// but IConfiguration is designed to only contain string data, so we end up with
		// an ExpandoObject full of strings.
		// Here, we try to coerce the data into suitable types.
		dynamic newExpando = new ExpandoObject();
		var newExpandoDictionary = (IDictionary<string, object?>)newExpando;
		foreach (var kvp in data) {
			var key = kvp.Key;
			var value = kvp.Value;
			CoerceType(value, v => newExpandoDictionary[key] = v);
		}
		return newExpando;
	}

	private static dynamic ToExpandoObject(this IConfiguration configuration) {
		var keyNames = configuration.GetChildren().Select(c => c.Key).ToArray();
		var allNumericKeys = keyNames.Length != 0 && keyNames.All(name => int.TryParse(name, out _));
		if (allNumericKeys) {
			var list = new List<dynamic>();
			configuration.Bind(list);
			list = CoerceTypes(list);
			return list;
		}
		var expandoObject = new ExpandoObject();
		configuration.Bind(expandoObject);
		expandoObject = CoerceTypes(expandoObject);
		return expandoObject;
	}

	public static OptionsBuilder<T> BindWithDynamics<T>(this OptionsBuilder<T> optionsBuilder, IConfiguration configuration)
		where T : class =>
		optionsBuilder
			.Bind(configuration)
			.Configure(options => options.BindDynamics(configuration));

	public static T BindDynamics<T>(this T options, IConfiguration configuration) {
		var configurations = configuration.AsEnumerable().ToList();
		var isEmptyConfiguration = configurations.Count == 1 && configurations[0].Value == null;
		if (options == null || isEmptyConfiguration)
			return options;
		var t = options.GetType();
		if (t.IsValueType)
			return options;
		// If the type is an ExpandoObject, it implements IDictionary<string,object?>
		if (options is ExpandoObject && options is IDictionary<string, object?> expandoDictionary) {
			// Find entries where the mapped object is a plain "object" with no properties.
			// This indicates properties that the default IConfiguration binder has failed to
			// dynamically bind (it uses a simple object as a fallback).
			var nonDescriptEntries = expandoDictionary.Where(kvp => kvp.Value?.GetType() == typeof(object)).ToList();
			// These non-descript entries should almost certainly be re-mapped.
			foreach (var key in nonDescriptEntries.Select(kvp => kvp.Key)) {
				var subsection = configuration.GetSection(key);
				expandoDictionary[key] = BindDynamics(subsection.ToExpandoObject(), subsection);
			}
		} else if (options is IDictionary dictionary) {
			// Dictionaries are keyed in the configuration by key name.
			foreach (var key in dictionary.Keys) {
				var value = dictionary[key];
				var keyString = $"{key}";
				if (value != null && !string.IsNullOrEmpty(keyString)) {
					var subsection = configuration.GetSection(keyString);
					BindDynamics(value, subsection);
				}
			}
		} else if (options is IEnumerable items) {
			// Non-dictionary collections are numerically indexed.
			var index = 0;
			foreach (var item in items) {
				var subsection = configuration.GetSection($"{index++}");
				BindDynamics(item, subsection);
			}
		} else {
			// This must be a non-collection, non-value, non-null, statically-typed object.
			var props = t.GetProperties();
			// No point checking value types (int, bool, etc) or common System.* types.
			var nonValueProps = props.Where(p => !p.PropertyType.IsValueType && !ExcludedPropertyTypes.Contains(p.PropertyType));
			foreach (var prop in nonValueProps) {
				var subsection = configuration.GetSection(prop.Name);
				var propValue = prop.GetValue(options);
				// If the property is null, then it came from the config that way. Leave it alone.
				// We're only interested in dynamic properties that were set (incorrectly) from the
				// config.
				if (propValue != null) {
					if (prop.CanWrite && prop.IsDynamic()) {
						propValue = subsection.ToExpandoObject();
						prop.SetValue(options, propValue);
					}
					// Even if we didn't find a dynamic property in this object type, sub-properties
					// might still contain them, so we still need to iterate ...
					BindDynamics(propValue, subsection);
				}
			}
		}
		return options;
	}
}

/// <summary>
/// Handy type extension.
/// </summary>
public static class PropertyInfoExtensions {
	/// <summary>
	/// Returns true if the property is a dynamic property.
	/// </summary>
	/// <param name="propInfo">Property to check.</param>
	/// <returns>True if dynamic</returns>
	public static bool IsDynamic(this PropertyInfo propInfo) =>
		// If a property is declared as e.g. ExpandoObject, then that implements IDynamicMetaObjectProvider,
		// so can definitely be considered to be dynamic.
		// However, if a property is declared as "dynamic", we have no runtime information, and the property
		// type is returned as "object".
		// However, there will be a sneaky custom attribute in the PropertyInfo ...
		typeof(IDynamicMetaObjectProvider).IsAssignableFrom(propInfo.PropertyType) ||
			propInfo.CustomAttributes.Any(x => x.AttributeType == typeof(DynamicAttribute));
}
