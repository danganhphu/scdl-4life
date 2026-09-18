using System.Reflection;

namespace Scdl.Core.Tests;

/// <summary>
/// Rules about the shape of the Core assembly that a reviewer would otherwise
/// have to remember. Reflection is fine here: the test host is the one project
/// that is deliberately not AOT.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly CoreAssembly = typeof(ICoreAssemblyMarker).Assembly;

    private static IEnumerable<Type> PublicTypes => CoreAssembly.GetExportedTypes().Where(type => !type.IsNested);

    [Test]
    public async Task Every_public_type_lives_under_the_root_namespace()
    {
        foreach (var type in PublicTypes)
        {
            await Assert.That(type.Namespace?.StartsWith("Scdl.Core", StringComparison.Ordinal) ?? false)
                        .IsTrue();
        }
    }

    /// <summary>
    /// ATL is an implementation detail of tagging. If one of its types ever
    /// appears on a public signature, swapping the tagger becomes a breaking
    /// change for every caller.
    /// </summary>
    [Test]
    public async Task No_public_signature_leaks_the_tagging_library()
    {
        foreach (var type in PublicTypes)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.DeclaringType != type)
                {
                    continue;
                }

                var leaks = method.ReturnType.Namespace?.StartsWith("ATL", StringComparison.Ordinal) ?? false;

                leaks |= method.GetParameters()
                               .Any(parameter => parameter.ParameterType.Namespace?
                                                     .StartsWith("ATL", StringComparison.Ordinal) ??
                                                 false);

                await Assert.That(leaks).IsFalse();
            }
        }
    }

    /// <summary>
    /// Every concrete service is sealed. Inheritance is not part of this
    /// design, and sealing lets the JIT and the AOT compiler devirtualise.
    /// </summary>
    [Test]
    public async Task Every_public_concrete_class_is_sealed()
    {
        var unsealed = PublicTypes
                       .Where(type => type is { IsClass: true, IsAbstract: false, IsSealed: false })
                       .Select(type => type.FullName)
                       .ToArray();

        await Assert.That(unsealed.Length).IsEqualTo(0);
    }

    [Test]
    public async Task The_marker_names_the_core_assembly()
    {
        await Assert.That(CoreAssembly.GetName().Name).IsEqualTo("Scdl.Core");
    }
}
