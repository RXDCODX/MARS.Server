using System;

namespace MARS.Server.Services.ServiceManager.Entitys;

/// <summary>
/// Атрибут для указания имени сервиса
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ServiceNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
