﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.WebTools.Languages.Css.Editor.Parser;
using Microsoft.WebTools.Languages.Css.Parser;
using Microsoft.WebTools.Languages.Css.TreeItems;
using Microsoft.WebTools.Languages.Css.TreeItems.AtDirectives;
using Microsoft.WebTools.Languages.Css.TreeItems.Selectors;
using Microsoft.WebTools.Languages.Shared.Editor.EditorHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace HtmlTools
{
    internal class ClassResultSource : IPeekResultSource
    {
        private readonly ClassDefinitionPeekItem peekableItem;

        public ClassResultSource(ClassDefinitionPeekItem peekableItem)
        {
            this.peekableItem = peekableItem;
        }

        public void FindResults(string relationshipName, IPeekResultCollection resultCollection, CancellationToken cancellationToken, IFindPeekResultsCallback callback)
        {
            if (relationshipName != PredefinedPeekRelationships.Definitions.Name)
            {
                return;
            }

            string file = FindRuleSetInFile(new[] { ".less", ".scss", ".css" }, peekableItem._className, out RuleSet rule);

            if (rule == null)
            {
                callback.ReportProgress(1);
                return;
            }

            using (var displayInfo = new PeekResultDisplayInfo(label: peekableItem._className, labelTooltip: file, title: Path.GetFileName(file), titleTooltip: file))
            {
                IDocumentPeekResult result = peekableItem._peekResultFactory.Create
                (
                    displayInfo,
                    file,
                    new Span(rule.Start, rule.Length),
                    rule.Start,
                    false
                );

                resultCollection.Add(result);
                callback.ReportProgress(1);
            }
        }

        private string FindRuleSetInFile(IEnumerable<string> extensions, string className, out RuleSet rule)
        {
            string root = ProjectHelpers.GetProjectFolder(peekableItem._textbuffer.GetFileName());
            string result = null;
            bool isLow = false, isMedium = false;
            rule = null;

            foreach (string ext in extensions)
            {
                ICssParser parser = CssParserLocator.FindComponent(ProjectHelpers.GetContentType(ext.Trim('.'))).CreateParser();

                foreach (string file in Directory.EnumerateFiles(root, "*" + ext, SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".min" + ext, StringComparison.OrdinalIgnoreCase) ||
                        file.Contains("node_modules") ||
                        file.Contains("bower_components") ||
                        file.Contains("\\obj\\Release") ||
                        file.Contains("\\obj\\Debug\\") ||
                        file.Contains("\\obj\\publish\\"))
                    {
                        continue;
                    }

                    string text = File.ReadAllText(file);
                    int index = text.IndexOf("." + className, StringComparison.Ordinal);

                    if (index == -1)
                    {
                        continue;
                    }

                    StyleSheet css = parser.Parse(text, true);
                    var visitor = new CssItemCollector<ClassSelector>(false);
                    css.Accept(visitor);

                    IEnumerable<ClassSelector> selectors = visitor.Items.Where(c => c.ClassName.Text == className);
                    ClassSelector high = selectors.FirstOrDefault(c => c.FindType<AtDirective>() == null && (c.Parent.NextSibling == null || c.Parent.NextSibling.Text == ","));

                    if (high != null)
                    {
                        rule = high.FindType<RuleSet>();
                        return file;
                    }

                    ClassSelector medium = selectors.FirstOrDefault(c => c.Parent.NextSibling == null || c.Parent.NextSibling.Text == ",");

                    if (medium != null && !isMedium)
                    {
                        rule = medium.FindType<RuleSet>();
                        result = file;
                        isMedium = true;
                        continue;
                    }

                    ClassSelector low = selectors.FirstOrDefault();

                    if (low != null && !isLow && !isMedium)
                    {
                        rule = low.FindType<RuleSet>();
                        result = file;
                        isLow = true;
                    }
                }
            }

            return result;
        }
    }
}
