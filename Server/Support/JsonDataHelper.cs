using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPC_Demo;

#region [Implementation]
/// <inheritdoc />
public class JsonDataHelper<T>(string filePath) : IJsonDataHelper<T>
{
    T? data;

    /// <inheritdoc />
    public event JsonDataChangedEvent<T>? DataChanging;

    /// <inheritdoc />
    public event JsonDataChangedEvent<T>? DataChanged;

    /// <summary>
    /// Adjust to your project's preferences.
    /// </summary>
    JsonSerializerOptions options = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new ModuleIdJsonConverter(), new TypeJsonConverter() }
    };

    /// <inheritdoc />
    public T? GetData()
    {
        // If we already have data then there is no need to read from disk again.
        if (data != null) { return data; }

        try
        {
            var json = System.IO.File.ReadAllText(filePath);
            return data = JsonSerializer.Deserialize<T>(json, options) ?? throw new FormatException($"Could not deserialize '{typeof(T).Name}'.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] JsonDataHelper.Get: {ex.Message}");
            return default;
        }
    }

    /// <inheritdoc />
    public void SaveData(T newData)
    {
        var oldData = data;
        DataChanging?.Invoke(this, data, newData);
        try
        {
            var json = JsonSerializer.Serialize(data = newData, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters = { new ModuleIdJsonConverter(), new TypeJsonConverter() }
            });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] JsonDataHelper.Save: {ex.Message}");
        }
        DataChanged?.Invoke(this, oldData, newData);
    }

    /// <inheritdoc />
    public JsonSerializerOptions GetOptions() => options;

    /// <inheritdoc />
    public void SetOptions(JsonSerializerOptions NewOptions) => options = NewOptions;
}
#endregion

#region [Interface]
/// <summary>
///   Service for reading and writing data.
/// </summary>
/// <typeparam name="T">the type of data to read/write</typeparam>
public interface IJsonDataHelper<T>
{
    /// <summary>
    ///   Get the data of type <typeparamref name="T"/> via 
    ///   <see cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)"/>.
    /// </summary>
    /// <returns><typeparamref name="T"/></returns>
    public T? GetData();

    /// <summary>
    ///   Save the data of type <typeparamref name="T"/> via 
    ///   <see cref="JsonSerializer.Serialize(object?, Type, JsonSerializerOptions?)"/>.
    /// </summary>
    /// <param name="data"><typeparamref name="T"/></param>
    public void SaveData(T data);

    /// <summary>
    ///   Fetches the <see cref="JsonSerializerOptions"/>.
    /// </summary>
    public JsonSerializerOptions GetOptions();

    /// <summary>
    ///   Modifies the <see cref="JsonSerializerOptions"/>.
    /// </summary>
    public void SetOptions(JsonSerializerOptions NewOptions);

    /// <summary>
    ///   Event that is raised before the data is changed.
    /// </summary>
    public event JsonDataChangedEvent<T> DataChanging;

    /// <summary>
    ///   Event that is raised after the data is changed.
    /// </summary>
    public event JsonDataChangedEvent<T> DataChanged;
}

/// <summary>
///   Event handler for data changing.
/// </summary>
/// <typeparam name="T">Type of data.</typeparam>
public delegate void JsonDataChangedEvent<in T>(object sender, T? oldData, T newData);
#endregion

#region [Custom Converters]
/// <summary>
///   Example of a custom converter for serialize/deserialize process.
/// </summary>
public class ModuleIdJsonConverter : JsonConverter<ModuleId>
{
    public override ModuleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (Enum.TryParse<ModuleId>(value, out var mid)) { return mid; }
        return ModuleId.None;
    }

    public override void Write(Utf8JsonWriter writer, ModuleId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"{value}");
    }
}

/// <summary>
///   Type conversion in JSON can be tricky.
///   This method is an attempt to read/write the type's fully qualified name.
/// </summary>
public class TypeJsonConverter : JsonConverter<Type>
{
    public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Read the string value and convert it back to Type
        string? typeName = reader.GetString();

        if (!string.IsNullOrEmpty(typeName))
            return Type.GetType(typeName);

        return null;
    }

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
    {
        // Write the full name of the type as a string
        writer.WriteStringValue(value.AssemblyQualifiedName);
    }
}
#endregion