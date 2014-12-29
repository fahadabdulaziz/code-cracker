﻿using System;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeCracker.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class JsonNetAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CC0054";
        internal const string Title = "Your Json syntax is wrong";
        internal const string MessageFormat = "{0}";
        internal const string Category = SupportedCategories.Usage;
        const string Description = "This diagnostic checks the json string and triggers if the parsing fail "
            + "by throwing an exception.";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description,
            helpLink: HelpLink.ForDiagnostic(DiagnosticId));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(c => Analyzer(c, "DeserializeObject", "Newtonsoft.Json.JsonConvert.DeserializeObject<T>(string)"), SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(c => Analyzer(c, "Parse", "Newtonsoft.Json.Linq.JObject.Parse(string)"), SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(c => Analyzer(c, "Parse", "Newtonsoft.Json.Linq.JArray.Parse(string)"), SyntaxKind.InvocationExpression);
        }

        private void Analyzer(SyntaxNodeAnalysisContext context, string methodName, string methodFullDefinition)
        {
            var invocationExpression = (InvocationExpressionSyntax)context.Node;

            var memberExpresion = invocationExpression.Expression as MemberAccessExpressionSyntax;
            if (memberExpresion?.Name?.Identifier.ValueText != methodName) return;

            var memberSymbol = context.SemanticModel.GetSymbolInfo(memberExpresion).Symbol;
            if (memberSymbol?.OriginalDefinition?.ToString() != methodFullDefinition) return;

            var argumentList = invocationExpression.ArgumentList;
            if ((argumentList?.Arguments.Count ?? 0) != 1) return;

            var literalParameter = argumentList.Arguments[0].Expression as LiteralExpressionSyntax;
            if (literalParameter == null) return;

            var jsonOpt = context.SemanticModel.GetConstantValue(literalParameter);
            var json = jsonOpt.Value as string;

            CheckJsonValue(context, literalParameter, json);
        }

        private static void CheckJsonValue(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literalParameter,
            string json)
        {
            try
            {
                _parseMethodInfo.Value.Invoke(null, new[] { json });
            }
            catch (Exception ex)
            {
                var diag = Diagnostic.Create(Rule, literalParameter.GetLocation(), ex.InnerException.Message);
                context.ReportDiagnostic(diag);
            }
        }

        private static Lazy<Type> _jObjectType = new Lazy<Type>(() => Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json"));
        private static Lazy<MethodInfo> _parseMethodInfo = new Lazy<MethodInfo>(() => _jObjectType.Value.GetRuntimeMethod("Parse", new[] { typeof(string) }));
    }
}