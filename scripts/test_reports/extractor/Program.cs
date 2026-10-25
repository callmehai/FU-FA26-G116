using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Parse C# syntax rather than matching attributes or braces with regular expressions.
var root = Path.GetFullPath(args[0]);
var records = new List<object>();
foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories).Order())
{
    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
    if (relative.Split('/').Any(p => p is "bin" or "obj")) continue;
    var source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source);
    var syntax = tree.GetRoot();
    foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>())
    {
        var attributes = method.AttributeLists.SelectMany(a => a.Attributes).ToArray();
        string AttributeName(AttributeSyntax a) => a.Name.ToString().Split('.').Last().Replace("Attribute", "");
        var marker = attributes.FirstOrDefault(a => AttributeName(a) is "Fact" or "Theory");
        if (marker is null) continue;
        var cls = method.Ancestors().OfType<ClassDeclarationSyntax>().First();
        var ns = string.Join(".", method.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
        var className = cls.Identifier.Text;
        var fullName = ns + "." + string.Join(".", method.Ancestors().OfType<ClassDeclarationSyntax>().Reverse().Select(c => c.Identifier.Text)) + "." + method.Identifier.Text;
        var signature = method.ParameterList.ToString();
        var id = "TC-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relative + ":" + fullName + signature)))[..12];
        var asserts = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(i => i.Expression.ToString().StartsWith("Assert.") && !i.Ancestors().OfType<InvocationExpressionSyntax>().Any(a => a.Expression.ToString().StartsWith("Assert."))).Select(i => i.ToString()).ToArray();
        records.Add(new { id, fullName, className, method = method.Identifier.Text,
            module = relative.Split('/').Length > 3 ? relative.Split('/')[2] : "Core",
            source = relative, line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1,
            kind = AttributeName(marker), parameters = signature,
            declaration = $"{method.Modifiers} {method.ReturnType} {method.Identifier}{method.ParameterList}",
            data = attributes.Where(a => AttributeName(a) is "InlineData" or "MemberData" or "ClassData").Select(a => a.ToString()).ToArray(),
            skip = marker.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "Skip")?.Expression.ToString(),
            assertions = asserts,
            calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(i => !i.Expression.ToString().StartsWith("Assert.")).Select(i => i.Expression.ToString()).Distinct().ToArray(),
            body = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "",
            documentation = method.GetLeadingTrivia().ToString(),
            context = string.Join("\n", cls.Members.Where(m => m is not MethodDeclarationSyntax md || !md.AttributeLists.SelectMany(a => a.Attributes).Any(a => AttributeName(a) is "Fact" or "Theory")).Select(m => m.ToString())),
            classDeclaration = className + cls.BaseList?.ToString(),
            sourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant() });
    }
}
File.WriteAllText(args[1], JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Extracted {records.Count} test methods.");
