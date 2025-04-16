using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

namespace LilToonCloner
{
    /// <summary>
    /// lilToon-Clonerのメインエディタウィンドウを提供するクラス
    /// ユーザーインターフェイスとユーザー操作の処理を担当
    /// </summary>
    public class LilToonClonerUI : EditorWindow
    {
        // UIリソース
        private GUIStyle boxOuter;
        private GUIStyle boxInnerHalf;
        private GUIStyle boxInner;
        private GUIStyle customBox;

        // スクロール位置
        private Vector2 mainScrollPos;
        private Vector2 targetsScrollPos;
        private Vector2 propertiesScrollPos;

        // 選択オブジェクトとマテリアル
        private GameObject selectedObject;
        private Material sourceMaterial;

        // 処理クラスへの参照
        private LilToonClonerProcessor processor;
        private LilToonClonerSelection selectionManager;

        // UI状態
        private bool showAdvancedOptions = false;
        private bool showBackupOptions = true;
        private bool createBackups = true;

        // メニューからウィンドウを開くためのエントリポイント
        [MenuItem("Tools/lilToon/lilToon-Cloner")]
        public static void ShowWindow()
        {
            LilToonClonerUI window = GetWindow<LilToonClonerUI>("lilToon-Cloner");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        private void OnEnable()
        {
            // 初期化
            processor = new LilToonClonerProcessor();
            selectionManager = new LilToonClonerSelection();
            InitializeGUIStyles();
        }

        private void InitializeGUIStyles()
        {
            boxOuter = LilToonClonerUtils.InitializeBox(4, 4, 2);
            boxInnerHalf = LilToonClonerUtils.InitializeBox(4, 2, 2);
            boxInner = LilToonClonerUtils.InitializeBox(4, 2, 2);
            customBox = LilToonClonerUtils.InitializeBox(6, 4, 4);

            // GUI用テクスチャを読み込む
            string resourcePath = "Assets/";
            // テクスチャファイルが見つかればロード、なければデフォルトスタイルを使用
            if (EditorGUIUtility.isProSkin)
            {
                // ダークテーマ用テクスチャの読み込み
                LilToonClonerUtils.TryLoadBackgroundTexture(boxOuter, resourcePath + "Editor/Resources/gui_box_outer_dark.png");
                LilToonClonerUtils.TryLoadBackgroundTexture(boxInnerHalf, resourcePath + "Editor/Resources/gui_box_inner_half_dark.png");
                LilToonClonerUtils.TryLoadBackgroundTexture(boxInner, resourcePath + "Editor/Resources/gui_box_inner_dark.png");
                LilToonClonerUtils.TryLoadBackgroundTexture(customBox, resourcePath + "Editor/Resources/gui_custom_box_dark.png");
            }
            else
            {
                // ライトテーマ用テクスチャの読み込み
                LilToonClonerUtils.TryLoadBackgroundTexture(boxOuter, resourcePath + "Editor/Resources/gui_box_outer_light.png");
                LilToonClonerUtils.TryLoadBackgroundTexture(boxInnerHalf, resourcePath + "Editor/Resources/gui_box_inner_half_light.png");
                LilToonClonerUtils.TryLoadBackgroundTexture(boxInner, resourcePath + "Editor/Resources/gui_box_inner_light.png");
                LilToonClonerUtils.TryLoadBackgroundTexture(customBox, resourcePath + "Editor/Resources/gui_custom_box_light.png");
            }
        }

        private void OnGUI()
        {
            if (boxOuter == null || boxInnerHalf == null || boxInner == null || customBox == null)
            {
                InitializeGUIStyles();
            }

            mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

            // ヘッダー
            GUILayout.Space(10);
            EditorGUILayout.LabelField("lilToon-Cloner", EditorStyles.boldLabel);
            GUILayout.Label("lilToonマテリアルの設定を一括コピーするツール", EditorStyles.miniLabel);
            GUILayout.Space(10);

            // ソースセクション
            EditorGUILayout.BeginVertical(boxOuter);
            EditorGUILayout.LabelField("ソース設定", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(boxInner);
            DrawSourceSection();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);

            // ターゲットセクション
            EditorGUILayout.BeginVertical(boxOuter);
            EditorGUILayout.LabelField("ターゲット設定", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(boxInner);
            DrawTargetSection();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);

            // プロパティセクション
            if (sourceMaterial != null)
            {
                EditorGUILayout.BeginVertical(boxOuter);
                EditorGUILayout.LabelField("コピーするプロパティ", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginVertical(boxInner);
                DrawPropertySection();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndVertical();
                
                GUILayout.Space(10);
            }

            // オプションセクション
            EditorGUILayout.BeginVertical(boxOuter);
            showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "詳細オプション", true);
            
            if (showAdvancedOptions)
            {
                EditorGUILayout.BeginVertical(boxInner);
                DrawAdvancedOptions();
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);

            // 実行ボタン
            DrawActionButtons();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceSection()
        {
            // ソースオブジェクト選択
            EditorGUILayout.LabelField("コピー元の選択", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            selectedObject = (GameObject)EditorGUILayout.ObjectField("ゲームオブジェクト", selectedObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                // オブジェクトが変更されたら、lilToon材質を見つける
                if (selectedObject != null)
                {
                    processor.FindLilToonMaterials(selectedObject);
                }
            }

            // ロードボタン
            if (GUILayout.Button("lilToonマテリアルをロード"))
            {
                if (selectedObject != null)
                {
                    processor.FindLilToonMaterials(selectedObject);
                    selectionManager.ClearSelection();
                }
                else
                {
                    EditorUtility.DisplayDialog("エラー", "ゲームオブジェクトを選択してください", "OK");
                }
            }

            // ソースマテリアル選択
            if (processor.FoundMaterials.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                sourceMaterial = (Material)EditorGUILayout.ObjectField("ソースマテリアル", sourceMaterial, typeof(Material), false);
                if (EditorGUI.EndChangeCheck() && sourceMaterial != null)
                {
                    // ソースマテリアルが変更されたらプロパティをロード
                    processor.InitializePropertyStates(sourceMaterial);
                }
            }
            else if (selectedObject != null)
            {
                EditorGUILayout.HelpBox("選択したオブジェクトにlilToonマテリアルが見つかりませんでした。", MessageType.Warning);
            }
        }

        private void DrawTargetSection()
        {
            // ターゲットのリスト表示とチェックボックス
            if (processor.FoundMaterials.Count > 0)
            {
                EditorGUILayout.LabelField("コピー先マテリアル", EditorStyles.boldLabel);

                // 全選択/全解除ボタン
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("全選択"))
                {
                    selectionManager.SelectAll();
                }
                if (GUILayout.Button("全解除"))
                {
                    selectionManager.DeselectAll();
                }
                if (GUILayout.Button("選択を反転"))
                {
                    selectionManager.InvertSelection();
                }
                EditorGUILayout.EndHorizontal();

                // スクロール可能なマテリアルリスト
                targetsScrollPos = EditorGUILayout.BeginScrollView(targetsScrollPos, GUILayout.Height(200));
                
                for (int i = 0; i < processor.FoundMaterials.Count; i++)
                {
                    Material material = processor.FoundMaterials[i];
                    if (material == null || material == sourceMaterial) continue;

                    EditorGUILayout.BeginHorizontal();
                    bool isSelected = selectionManager.IsSelected(i);
                    bool newSelection = EditorGUILayout.ToggleLeft(material.name, isSelected);
                    
                    if (newSelection != isSelected)
                    {
                        selectionManager.SetSelection(i, newSelection);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();

                // 選択されたマテリアルのカウント表示
                int selectedCount = selectionManager.GetSelectionCount();
                EditorGUILayout.LabelField($"選択済み: {selectedCount} / {processor.FoundMaterials.Count - 1}");
            }
        }

        private void DrawPropertySection()
        {
            if (sourceMaterial == null) return;

            // コピーするプロパティのカテゴリとチェックボックス
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全プロパティを選択"))
            {
                processor.SelectAllProperties();
            }
            if (GUILayout.Button("全プロパティを解除"))
            {
                processor.DeselectAllProperties();
            }
            EditorGUILayout.EndHorizontal();

            // プロパティのカテゴリ別表示
            propertiesScrollPos = EditorGUILayout.BeginScrollView(propertiesScrollPos, GUILayout.Height(300));
            
            processor.DrawPropertiesUI(sourceMaterial);
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawAdvancedOptions()
        {
            showBackupOptions = EditorGUILayout.Foldout(showBackupOptions, "バックアップオプション", true);
            if (showBackupOptions)
            {
                createBackups = EditorGUILayout.Toggle("バックアップを作成", createBackups);
                EditorGUILayout.HelpBox("有効にすると、変更前のマテリアルのバックアップを作成します。", MessageType.Info);
            }

            // その他の詳細オプション
            processor.UseParallelProcessing = EditorGUILayout.Toggle("並列処理を使用", processor.UseParallelProcessing);
            EditorGUILayout.HelpBox("有効にすると、複数のマテリアルを同時に処理します。大量のマテリアルがある場合に高速化されます。", MessageType.Info);
        }

        private void DrawActionButtons()
        {
            bool hasSelectedTargets = selectionManager.GetSelectionCount() > 0;
            bool hasSourceMaterial = sourceMaterial != null;
            bool canExecute = hasSelectedTargets && hasSourceMaterial;

            // 実行ボタン - 条件を満たしている場合のみ有効化
            EditorGUI.BeginDisabledGroup(!canExecute);
            
            if (GUILayout.Button("選択したプロパティをコピー", GUILayout.Height(40)))
            {
                ExecuteCopyProcess();
            }
            
            EditorGUI.EndDisabledGroup();

            // 実行できない理由をヘルプボックスで表示
            if (!hasSourceMaterial)
            {
                EditorGUILayout.HelpBox("ソースマテリアルを選択してください。", MessageType.Warning);
            }
            else if (!hasSelectedTargets)
            {
                EditorGUILayout.HelpBox("ターゲットマテリアルを少なくとも1つ選択してください。", MessageType.Warning);
            }
        }

        private void ExecuteCopyProcess()
        {
            try
            {
                // 選択されたマテリアルを取得
                List<Material> targetMaterials = new List<Material>();
                for (int i = 0; i < processor.FoundMaterials.Count; i++)
                {
                    if (selectionManager.IsSelected(i) && processor.FoundMaterials[i] != sourceMaterial)
                    {
                        targetMaterials.Add(processor.FoundMaterials[i]);
                    }
                }

                if (targetMaterials.Count == 0)
                {
                    EditorUtility.DisplayDialog("警告", "コピー先マテリアルが選択されていません。", "OK");
                    return;
                }

                // バックアップが有効な場合はバックアップを作成
                if (createBackups)
                {
                    processor.BackupMaterials(targetMaterials);
                }

                // プロパティのコピーを実行
                int processedCount = processor.CopySelectedProperties(sourceMaterial, targetMaterials);

                // 結果を表示
                EditorUtility.DisplayDialog("完了", $"{processedCount}個のマテリアルに対してプロパティをコピーしました。", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"コピー処理中にエラーが発生しました: {ex.Message}");
                EditorUtility.DisplayDialog("エラー", $"コピー処理中にエラーが発生しました: {ex.Message}", "OK");
            }
        }
    }
}