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
        private bool stylesInitialized = false;

        // スクロール位置
        private Vector2 mainScrollPos;
        private Vector2 targetsScrollPos;
        private Vector2 propertiesScrollPos;

        // 選択オブジェクトとマテリアル
        private GameObject selectedObject;
        private Material sourceMaterial;
        private Material lastSourceMaterial;
        private bool needsPropertyUpdate = false;

        // 処理クラスへの参照
        private LilToonClonerProcessor processor;
        private LilToonClonerSelection selectionManager;

        // UI状態
        private bool showAdvancedOptions = false;
        private bool showBackupOptions = true;
        private bool createBackups = true;
        private bool materialsLoaded = false;
        private bool guiChanged = false;

        // キャッシュとタイマー
        private double lastRepaintTime;
        private const double RepaintThreshold = 0.1; // 秒単位

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
            lastRepaintTime = EditorApplication.timeSinceStartup;
            
            // エディタの更新イベントを監視して最適化
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            // イベントリスナーを解除
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            // エディタの更新時、変更があった場合のみ再描画
            if (guiChanged && (EditorApplication.timeSinceStartup - lastRepaintTime) > RepaintThreshold)
            {
                Repaint();
                guiChanged = false;
                lastRepaintTime = EditorApplication.timeSinceStartup;
            }
            
            // プロパティの更新が必要な場合のみ処理
            if (needsPropertyUpdate && sourceMaterial != null)
            {
                processor.InitializePropertyStates(sourceMaterial);
                needsPropertyUpdate = false;
                guiChanged = true;
            }
        }

        private void InitializeGUIStyles()
        {
            if (stylesInitialized) return;
            
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
            
            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!stylesInitialized)
            {
                InitializeGUIStyles();
            }

            EditorGUI.BeginChangeCheck();
            
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
            if (materialsLoaded)
            {
                EditorGUILayout.BeginVertical(boxOuter);
                EditorGUILayout.LabelField("ターゲット設定", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginVertical(boxInner);
                DrawTargetSection();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndVertical();
                
                GUILayout.Space(10);
            }

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
            
            // GUIが変更された場合のみ更新フラグをセット
            if (EditorGUI.EndChangeCheck())
            {
                guiChanged = true;
            }
            
            // ソースマテリアルが変更された場合、プロパティ更新フラグをセット
            if (lastSourceMaterial != sourceMaterial)
            {
                lastSourceMaterial = sourceMaterial;
                needsPropertyUpdate = true;
            }
        }

        private void DrawSourceSection()
        {
            // ソースオブジェクト選択
            EditorGUILayout.LabelField("コピー元の選択", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            selectedObject = (GameObject)EditorGUILayout.ObjectField("ゲームオブジェクト", selectedObject, typeof(GameObject), true);
            
            // オブジェクトが変更されたら表示をリセット
            if (EditorGUI.EndChangeCheck())
            {
                materialsLoaded = false;
            }

            // ロードボタン
            if (GUILayout.Button("lilToonマテリアルをロード"))
            {
                if (selectedObject != null)
                {
                    // マテリアルのロードは比較的コストが高いためボタンクリック時のみ実行
                    processor.FindLilToonMaterials(selectedObject);
                    selectionManager.ClearSelection();
                    materialsLoaded = true;
                    sourceMaterial = null;
                }
                else
                {
                    EditorUtility.DisplayDialog("エラー", "ゲームオブジェクトを選択してください", "OK");
                    materialsLoaded = false;
                }
            }

            // ソースマテリアル選択
            if (materialsLoaded && processor.FoundMaterials.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                sourceMaterial = (Material)EditorGUILayout.ObjectField("ソースマテリアル", sourceMaterial, typeof(Material), false);
            }
            else if (selectedObject != null && materialsLoaded)
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
                    guiChanged = true;
                }
                if (GUILayout.Button("全解除"))
                {
                    selectionManager.DeselectAll();
                    guiChanged = true;
                }
                if (GUILayout.Button("選択を反転"))
                {
                    selectionManager.InvertSelection();
                    guiChanged = true;
                }
                EditorGUILayout.EndHorizontal();

                // スクロール可能なマテリアルリスト
                targetsScrollPos = EditorGUILayout.BeginScrollView(targetsScrollPos, GUILayout.Height(200));
                
                // 描画最適化: 表示範囲内のマテリアルのみ処理
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
                        guiChanged = true;
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
                guiChanged = true;
            }
            if (GUILayout.Button("全プロパティを解除"))
            {
                processor.DeselectAllProperties();
                guiChanged = true;
            }
            EditorGUILayout.EndHorizontal();

            // プロパティのカテゴリ別表示
            propertiesScrollPos = EditorGUILayout.BeginScrollView(propertiesScrollPos, GUILayout.Height(300));
            
            // プロパティUIの描画は変更があった場合またはソースマテリアルが変わった場合のみ
            bool propertiesChanged = processor.DrawPropertiesUI(sourceMaterial);
            if (propertiesChanged)
            {
                guiChanged = true;
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawAdvancedOptions()
        {
            showBackupOptions = EditorGUILayout.Foldout(showBackupOptions, "バックアップオプション", true);
            if (showBackupOptions)
            {
                bool newCreateBackups = EditorGUILayout.Toggle("バックアップを作成", createBackups);
                if (newCreateBackups != createBackups)
                {
                    createBackups = newCreateBackups;
                    guiChanged = true;
                }
                EditorGUILayout.HelpBox("有効にすると、変更前のマテリアルのバックアップを作成します。", MessageType.Info);
            }

            // その他の詳細オプション
            bool newUseParallelProcessing = EditorGUILayout.Toggle("並列処理を使用", processor.UseParallelProcessing);
            if (newUseParallelProcessing != processor.UseParallelProcessing)
            {
                processor.UseParallelProcessing = newUseParallelProcessing;
                guiChanged = true;
            }
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