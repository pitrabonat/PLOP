using System.Collections.Generic;
using Plop.Presentation;
using UnityEditor;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>Editor report of every cosmetic content problem. Menu: PLOP → Characters → Validate Cosmetics.</summary>
    public sealed class CosmeticValidatorWindow : EditorWindow
    {
        private List<string> _issues;
        private Vector2 _scroll;
        private int _items;

        [MenuItem("PLOP/Characters/Validate Cosmetics", priority = 21)]
        public static void Open()
        {
            var w = GetWindow<CosmeticValidatorWindow>("Cosmetic Validator");
            w.Run();
        }

        /// <summary>Batch entry: -executeMethod Plop.CharacterTools.CosmeticValidatorWindow.RunBatch (exit 1 on problems).</summary>
        public static void RunBatch()
        {
            var catalog = CosmeticCatalogLoader.Load();
            var issues = CosmeticContentValidator.Run(catalog);
            foreach (var issue in issues) Debug.LogError("[PLOP] Cosmetic: " + issue);
            Debug.Log("[PLOP] Cosmetic validation: " + catalog.Items.Count + " items, " + issues.Count + " problems.");
            if (Application.isBatchMode) EditorApplication.Exit(issues.Count == 0 ? 0 : 1);
        }

        private void Run()
        {
            var catalog = CosmeticCatalogLoader.Load();
            _items = catalog.Items.Count;
            _issues = CosmeticContentValidator.Run(catalog);
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Validate again")) Run();
            if (_issues == null) return;
            if (_issues.Count == 0)
            {
                EditorGUILayout.HelpBox(_items + " cosmetic items: no problems found.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox(_issues.Count + " problems in " + _items + " items.", MessageType.Error);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var issue in _issues) EditorGUILayout.LabelField(issue, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }
    }
}
