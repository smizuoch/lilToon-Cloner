using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System;

namespace LilToonCloner
{
    /// <summary>
    /// lilToon-Clonerのメイン処理ロジックを担当するクラス
    /// マテリアルの検索、プロパティの管理、コピー処理を実行する
    /// </summary>
    public class LilToonClonerProcessor
    {
        // lilToonマテリアルのタイプ
        private enum MaterialPropertyType
        {
            Int,
            Float,
            Color,
            Vector,
            Texture,
            Enum,
            Cube
        }

        // マテリアルプロパティの情報
        private class PropertyInfo
        {
            public string Key;
            public string DisplayName;
            public MaterialPropertyType Type;
            public bool HasRange;
            public float MinValue;
            public float MaxValue;
            public string[] EnumOptions;
            public bool IsChecked;

            public PropertyInfo(string key, string displayName, MaterialPropertyType type, 
                bool hasRange = false, float minValue = 0f, float maxValue = 1f, string[] enumOptions = null)
            {
                Key = key;
                DisplayName = displayName;
                Type = type;
                HasRange = hasRange;
                MinValue = minValue;
                MaxValue = maxValue;
                EnumOptions = enumOptions;
                IsChecked = false;
            }
        }

        // カテゴリごとのプロパティグループ
        private class PropertyCategory
        {
            public string Name;
            public bool IsExpanded;
            public bool IsEnabled;
            public string EnableKey;
            public List<PropertyInfo> Properties;

            public PropertyCategory(string name, bool isExpanded = false, string enableKey = null)
            {
                Name = name;
                IsExpanded = isExpanded;
                EnableKey = enableKey;
                IsEnabled = true;
                Properties = new List<PropertyInfo>();
            }
        }

        // マテリアルのリストとプロパティ
        private List<Material> foundMaterials = new List<Material>();
        private Dictionary<string, PropertyCategory> propertyCategories = new Dictionary<string, PropertyCategory>();
        
        // フォールドアウト状態の管理
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        
        // 処理オプション
        private bool useParallelProcessing = true;
        private const string BackupBasePath = "Assets/Backups";
        
        // キャッシュ
        private Dictionary<Material, Dictionary<string, object>> propertyCache = new Dictionary<Material, Dictionary<string, object>>();
        private Material lastProcessedMaterial;
        private int propertyCacheVersion = 0;

        // プロパティ
        public List<Material> FoundMaterials => foundMaterials;
        public bool UseParallelProcessing 
        { 
            get => useParallelProcessing; 
            set => useParallelProcessing = value; 
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public LilToonClonerProcessor()
        {
            InitializePropertyCategories();
        }

        /// <summary>
        /// 指定したゲームオブジェクトからlilToonマテリアルを検索する
        /// </summary>
        /// <param name="gameObject">検索対象のゲームオブジェクト</param>
        public void FindLilToonMaterials(GameObject gameObject)
        {
            foundMaterials.Clear();
            propertyCache.Clear();
            propertyCacheVersion++;

            if (gameObject == null)
            {
                LilToonClonerUtils.LogWarning("ゲームオブジェクトが指定されていません。");
                return;
            }

            try
            {
                // レンダラーコンポーネントからマテリアルを取得
                Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
                HashSet<Material> uniqueMaterials = new HashSet<Material>();

                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (LilToonClonerUtils.IsValidLilToonMaterial(material) && !uniqueMaterials.Contains(material))
                        {
                            uniqueMaterials.Add(material);
                        }
                    }
                }

                foundMaterials = uniqueMaterials.ToList();

                if (foundMaterials.Count == 0)
                {
                    LilToonClonerUtils.LogWarning($"'{gameObject.name}'にlilToonマテリアルが見つかりませんでした。");
                }
                else
                {
                    LilToonClonerUtils.LogInfo($"{foundMaterials.Count}個のlilToonマテリアルが見つかりました。");
                }
            }
            catch (Exception ex)
            {
                LilToonClonerUtils.LogError($"マテリアル検索中にエラーが発生しました: {ex.Message}");
                foundMaterials.Clear();
            }
        }

        /// <summary>
        /// プロパティの状態を初期化する
        /// </summary>
        /// <param name="material">初期化の基準となるマテリアル</param>
        public void InitializePropertyStates(Material material)
        {
            if (material == null)
            {
                LilToonClonerUtils.LogWarning("マテリアルが指定されていません。");
                return;
            }

            // 同じマテリアルの場合は再初期化しない（パフォーマンス最適化）
            if (material == lastProcessedMaterial) return;
            lastProcessedMaterial = material;

            try
            {
                // カテゴリの有効状態を更新
                foreach (var category in propertyCategories.Values)
                {
                    if (!string.IsNullOrEmpty(category.EnableKey) && material.HasProperty(category.EnableKey))
                    {
                        category.IsEnabled = material.GetInt(category.EnableKey) == 1;
                    }
                }
                
                // プロパティキャッシュの初期化
                if (!propertyCache.ContainsKey(material))
                {
                    propertyCache[material] = new Dictionary<string, object>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"プロパティ初期化中にエラーが発生しました: {ex.Message}");
            }
        }

        /// <summary>
        /// プロパティカテゴリを初期化する
        /// </summary>
        private void InitializePropertyCategories()
        {
            propertyCategories.Clear();

            // ベース設定
            var baseCategory = new PropertyCategory("Base Settings");
            baseCategory.Properties.Add(new PropertyInfo("_LightMinLimit", "Light Min Limit", MaterialPropertyType.Float, true, 0f, 1f));
            baseCategory.Properties.Add(new PropertyInfo("_LightMaxLimit", "Light Max Limit", MaterialPropertyType.Float, true, 0f, 10f));
            baseCategory.Properties.Add(new PropertyInfo("_MonochromeLighting", "Monochrome Lighting", MaterialPropertyType.Float, true, 0f, 1f));
            baseCategory.Properties.Add(new PropertyInfo("_AsUnlit", "As Unlit", MaterialPropertyType.Float, true, 0f, 1f));
            baseCategory.Properties.Add(new PropertyInfo("_VertexLightStrength", "Vertex Light Strength", MaterialPropertyType.Float, true, 0f, 1f));
            baseCategory.Properties.Add(new PropertyInfo("_ShadowEnvStrength", "Shadow Environment Strength", MaterialPropertyType.Float, true, 0f, 1f));
            baseCategory.Properties.Add(new PropertyInfo("_BeforeExposureLimit", "Before Exposure Limit", MaterialPropertyType.Float));
            baseCategory.Properties.Add(new PropertyInfo("_lilDirectionalLightStrength", "Directional Light Strength", MaterialPropertyType.Float, true, 0f, 1f));
            propertyCategories.Add("Base", baseCategory);

            // メインカラー
            var mainCategory = new PropertyCategory("Main Color");
            mainCategory.Properties.Add(new PropertyInfo("_Color", "Color", MaterialPropertyType.Color));
            mainCategory.Properties.Add(new PropertyInfo("_MainTex", "Main Texture", MaterialPropertyType.Texture));
            mainCategory.Properties.Add(new PropertyInfo("_MainTex_ScrollRotate", "Scroll Rotate", MaterialPropertyType.Vector));
            mainCategory.Properties.Add(new PropertyInfo("_MainTexHSVG", "HSVG", MaterialPropertyType.Vector));
            mainCategory.Properties.Add(new PropertyInfo("_MainGradationStrength", "Gradation Strength", MaterialPropertyType.Float, true, 0f, 1f));
            mainCategory.Properties.Add(new PropertyInfo("_MainGradationTex", "Gradation Map", MaterialPropertyType.Texture));
            mainCategory.Properties.Add(new PropertyInfo("_MainColorAdjustMask", "Adjust Mask", MaterialPropertyType.Texture));
            propertyCategories.Add("Main", mainCategory);

            // メインカラー2nd
            var main2ndCategory = new PropertyCategory("Main Color 2nd", false, "_UseMain2ndTex");
            main2ndCategory.Properties.Add(new PropertyInfo("_UseMain2ndTex", "Use Main 2nd", MaterialPropertyType.Int));
            main2ndCategory.Properties.Add(new PropertyInfo("_Color2nd", "Color", MaterialPropertyType.Color));
            main2ndCategory.Properties.Add(new PropertyInfo("_Main2ndTex", "Texture", MaterialPropertyType.Texture));
            main2ndCategory.Properties.Add(new PropertyInfo("_Main2ndTexAngle", "Angle", MaterialPropertyType.Float, true, 0f, 360f));
            main2ndCategory.Properties.Add(new PropertyInfo("_Main2ndTex_ScrollRotate", "Scroll Rotate", MaterialPropertyType.Vector));
            propertyCategories.Add("Main2nd", main2ndCategory);

            // メインカラー3rd
            var main3rdCategory = new PropertyCategory("Main Color 3rd", false, "_UseMain3rdTex");
            main3rdCategory.Properties.Add(new PropertyInfo("_UseMain3rdTex", "Use Main 3rd", MaterialPropertyType.Int));
            main3rdCategory.Properties.Add(new PropertyInfo("_Color3rd", "Color", MaterialPropertyType.Color));
            main3rdCategory.Properties.Add(new PropertyInfo("_Main3rdTex", "Texture", MaterialPropertyType.Texture));
            main3rdCategory.Properties.Add(new PropertyInfo("_Main3rdTexAngle", "Angle", MaterialPropertyType.Float, true, 0f, 360f));
            main3rdCategory.Properties.Add(new PropertyInfo("_Main3rdTex_ScrollRotate", "Scroll Rotate", MaterialPropertyType.Vector));
            propertyCategories.Add("Main3rd", main3rdCategory);

            // 影設定
            var shadowCategory = new PropertyCategory("Shadows", false, "_UseShadow");
            shadowCategory.Properties.Add(new PropertyInfo("_UseShadow", "Use Shadow", MaterialPropertyType.Int));
            shadowCategory.Properties.Add(new PropertyInfo("_ShadowStrength", "Strength", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_ShadowColor", "Shadow Color", MaterialPropertyType.Color));
            shadowCategory.Properties.Add(new PropertyInfo("_ShadowColorTex", "Shadow Color Texture", MaterialPropertyType.Texture));
            shadowCategory.Properties.Add(new PropertyInfo("_ShadowNormalStrength", "Normal Strength", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_ShadowBorder", "Border", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_ShadowBlur", "Blur", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow2ndColor", "2nd Color", MaterialPropertyType.Color));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow2ndColorTex", "2nd Color Texture", MaterialPropertyType.Texture));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow2ndNormalStrength", "2nd Normal Strength", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow2ndBorder", "2nd Border", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow2ndBlur", "2nd Blur", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow3rdColor", "3rd Color", MaterialPropertyType.Color));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow3rdColorTex", "3rd Color Texture", MaterialPropertyType.Texture));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow3rdNormalStrength", "3rd Normal Strength", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow3rdBorder", "3rd Border", MaterialPropertyType.Float, true, 0f, 1f));
            shadowCategory.Properties.Add(new PropertyInfo("_Shadow3rdBlur", "3rd Blur", MaterialPropertyType.Float, true, 0f, 1f));
            propertyCategories.Add("Shadow", shadowCategory);

            // エミッション
            var emissionCategory = new PropertyCategory("Emission", false, "_UseEmission");
            emissionCategory.Properties.Add(new PropertyInfo("_UseEmission", "Use Emission", MaterialPropertyType.Int));
            emissionCategory.Properties.Add(new PropertyInfo("_EmissionColor", "Color", MaterialPropertyType.Color));
            emissionCategory.Properties.Add(new PropertyInfo("_EmissionMap", "Texture", MaterialPropertyType.Texture));
            emissionCategory.Properties.Add(new PropertyInfo("_EmissionMap_ScrollRotate", "Scroll Rotate", MaterialPropertyType.Vector));
            emissionCategory.Properties.Add(new PropertyInfo("_EmissionBlend", "Blend", MaterialPropertyType.Float, true, 0f, 1f));
            emissionCategory.Properties.Add(new PropertyInfo("_EmissionBlendMask", "Mask", MaterialPropertyType.Texture));
            propertyCategories.Add("Emission", emissionCategory);

            // エミッション2nd
            var emission2ndCategory = new PropertyCategory("Emission 2nd", false, "_UseEmission2nd");
            emission2ndCategory.Properties.Add(new PropertyInfo("_UseEmission2nd", "Use Emission 2nd", MaterialPropertyType.Int));
            emission2ndCategory.Properties.Add(new PropertyInfo("_Emission2ndColor", "Color", MaterialPropertyType.Color));
            emission2ndCategory.Properties.Add(new PropertyInfo("_Emission2ndMap", "Texture", MaterialPropertyType.Texture));
            emission2ndCategory.Properties.Add(new PropertyInfo("_Emission2ndMap_ScrollRotate", "Scroll Rotate", MaterialPropertyType.Vector));
            emission2ndCategory.Properties.Add(new PropertyInfo("_Emission2ndBlend", "Blend", MaterialPropertyType.Float, true, 0f, 1f));
            emission2ndCategory.Properties.Add(new PropertyInfo("_Emission2ndBlendMask", "Mask", MaterialPropertyType.Texture));
            propertyCategories.Add("Emission2nd", emission2ndCategory);

            // ノーマルマップ
            var normalCategory = new PropertyCategory("Normal Map", false, "_UseBumpMap");
            normalCategory.Properties.Add(new PropertyInfo("_UseBumpMap", "Use Normal Map", MaterialPropertyType.Int));
            normalCategory.Properties.Add(new PropertyInfo("_BumpMap", "Normal Map", MaterialPropertyType.Texture));
            normalCategory.Properties.Add(new PropertyInfo("_BumpScale", "Normal Scale", MaterialPropertyType.Float, true, -10f, 10f));
            propertyCategories.Add("Normal", normalCategory);

            // ノーマルマップ2nd
            var normal2ndCategory = new PropertyCategory("Normal Map 2nd", false, "_UseBump2ndMap");
            normal2ndCategory.Properties.Add(new PropertyInfo("_UseBump2ndMap", "Use Normal Map 2nd", MaterialPropertyType.Int));
            normal2ndCategory.Properties.Add(new PropertyInfo("_Bump2ndMap", "Normal Map", MaterialPropertyType.Texture));
            normal2ndCategory.Properties.Add(new PropertyInfo("_Bump2ndScale", "Normal Scale", MaterialPropertyType.Float, true, -10f, 10f));
            propertyCategories.Add("Normal2nd", normal2ndCategory);

            // 反射
            var reflectionCategory = new PropertyCategory("Reflection", false, "_UseReflection");
            reflectionCategory.Properties.Add(new PropertyInfo("_UseReflection", "Use Reflection", MaterialPropertyType.Int));
            reflectionCategory.Properties.Add(new PropertyInfo("_Smoothness", "Smoothness", MaterialPropertyType.Float, true, 0f, 1f));
            reflectionCategory.Properties.Add(new PropertyInfo("_SmoothnessTex", "Smoothness Texture", MaterialPropertyType.Texture));
            reflectionCategory.Properties.Add(new PropertyInfo("_Metallic", "Metallic", MaterialPropertyType.Float, true, 0f, 1f));
            reflectionCategory.Properties.Add(new PropertyInfo("_MetallicGlossMap", "Metallic Texture", MaterialPropertyType.Texture));
            reflectionCategory.Properties.Add(new PropertyInfo("_ReflectionColor", "Color", MaterialPropertyType.Color));
            reflectionCategory.Properties.Add(new PropertyInfo("_ReflectionColorTex", "Color Texture", MaterialPropertyType.Texture));
            propertyCategories.Add("Reflection", reflectionCategory);

            // MatCap
            var matCapCategory = new PropertyCategory("MatCap", false, "_UseMatCap");
            matCapCategory.Properties.Add(new PropertyInfo("_UseMatCap", "Use MatCap", MaterialPropertyType.Int));
            matCapCategory.Properties.Add(new PropertyInfo("_MatCapColor", "Color", MaterialPropertyType.Color));
            matCapCategory.Properties.Add(new PropertyInfo("_MatCapTex", "Texture", MaterialPropertyType.Texture));
            matCapCategory.Properties.Add(new PropertyInfo("_MatCapBlend", "Blend", MaterialPropertyType.Float, true, 0f, 1f));
            matCapCategory.Properties.Add(new PropertyInfo("_MatCapBlendMask", "Mask", MaterialPropertyType.Texture));
            matCapCategory.Properties.Add(new PropertyInfo("_MatCapEnableLighting", "Enable Lighting", MaterialPropertyType.Float, true, 0f, 1f));
            matCapCategory.Properties.Add(new PropertyInfo("_MatCapShadowMask", "Shadow Mask", MaterialPropertyType.Float, true, 0f, 1f));
            matCapCategory.Properties.Add(new PropertyInfo("_MatCapLod", "Blur", MaterialPropertyType.Float, true, 0f, 10f));
            propertyCategories.Add("MatCap", matCapCategory);

            // MatCap 2nd
            var matCap2ndCategory = new PropertyCategory("MatCap 2nd", false, "_UseMatCap2nd");
            matCap2ndCategory.Properties.Add(new PropertyInfo("_UseMatCap2nd", "Use MatCap 2nd", MaterialPropertyType.Int));
            matCap2ndCategory.Properties.Add(new PropertyInfo("_MatCap2ndColor", "Color", MaterialPropertyType.Color));
            matCap2ndCategory.Properties.Add(new PropertyInfo("_MatCap2ndTex", "Texture", MaterialPropertyType.Texture));
            matCap2ndCategory.Properties.Add(new PropertyInfo("_MatCap2ndBlend", "Blend", MaterialPropertyType.Float, true, 0f, 1f));
            matCap2ndCategory.Properties.Add(new PropertyInfo("_MatCap2ndBlendMask", "Mask", MaterialPropertyType.Texture));
            matCap2ndCategory.Properties.Add(new PropertyInfo("_MatCap2ndEnableLighting", "Enable Lighting", MaterialPropertyType.Float, true, 0f, 1f));
            matCap2ndCategory.Properties.Add(new PropertyInfo("_MatCap2ndShadowMask", "Shadow Mask", MaterialPropertyType.Float, true, 0f, 1f));
            matCap2ndCategory.Properties.Add(new PropertyInfo("_MatCap2ndLod", "Blur", MaterialPropertyType.Float, true, 0f, 10f));
            propertyCategories.Add("MatCap2nd", matCap2ndCategory);

            // リムライト
            var rimCategory = new PropertyCategory("Rim Light", false, "_UseRim");
            rimCategory.Properties.Add(new PropertyInfo("_UseRim", "Use Rim Light", MaterialPropertyType.Int));
            rimCategory.Properties.Add(new PropertyInfo("_RimColor", "Color", MaterialPropertyType.Color));
            rimCategory.Properties.Add(new PropertyInfo("_RimColorTex", "Texture", MaterialPropertyType.Texture));
            rimCategory.Properties.Add(new PropertyInfo("_RimBorder", "Border", MaterialPropertyType.Float, true, 0f, 1f));
            rimCategory.Properties.Add(new PropertyInfo("_RimBlur", "Blur", MaterialPropertyType.Float, true, 0f, 1f));
            rimCategory.Properties.Add(new PropertyInfo("_RimFresnelPower", "Fresnel Power", MaterialPropertyType.Float, true, 0.01f, 50f));
            rimCategory.Properties.Add(new PropertyInfo("_RimEnableLighting", "Enable Lighting", MaterialPropertyType.Float, true, 0f, 1f));
            rimCategory.Properties.Add(new PropertyInfo("_RimShadowMask", "Shadow Mask", MaterialPropertyType.Float, true, 0f, 1f));
            propertyCategories.Add("Rim", rimCategory);

            // グリッター
            var glitterCategory = new PropertyCategory("Glitter", false, "_UseGlitter");
            glitterCategory.Properties.Add(new PropertyInfo("_UseGlitter", "Use Glitter", MaterialPropertyType.Int));
            glitterCategory.Properties.Add(new PropertyInfo("_GlitterColor", "Color", MaterialPropertyType.Color));
            glitterCategory.Properties.Add(new PropertyInfo("_GlitterColorTex", "Texture", MaterialPropertyType.Texture));
            glitterCategory.Properties.Add(new PropertyInfo("_GlitterMainStrength", "Main Color Power", MaterialPropertyType.Float, true, 0f, 1f));
            glitterCategory.Properties.Add(new PropertyInfo("_GlitterEnableLighting", "Enable Lighting", MaterialPropertyType.Float, true, 0f, 1f));
            glitterCategory.Properties.Add(new PropertyInfo("_GlitterShadowMask", "Shadow Mask", MaterialPropertyType.Float, true, 0f, 1f));
            glitterCategory.Properties.Add(new PropertyInfo("_GlitterParams1", "Parameters 1", MaterialPropertyType.Vector));
            glitterCategory.Properties.Add(new PropertyInfo("_GlitterParams2", "Parameters 2", MaterialPropertyType.Vector));
            propertyCategories.Add("Glitter", glitterCategory);
        }

        /// <summary>
        /// すべてのプロパティを選択する
        /// </summary>
        public void SelectAllProperties()
        {
            foreach (var category in propertyCategories.Values)
            {
                foreach (var property in category.Properties)
                {
                    property.IsChecked = true;
                }
            }
        }

        /// <summary>
        /// すべてのプロパティの選択を解除する
        /// </summary>
        public void DeselectAllProperties()
        {
            foreach (var category in propertyCategories.Values)
            {
                foreach (var property in category.Properties)
                {
                    property.IsChecked = false;
                }
            }
        }

        /// <summary>
        /// プロパティUIを描画する
        /// </summary>
        /// <param name="material">対象のマテリアル</param>
        /// <returns>プロパティの選択状態が変更されたかどうか</returns>
        public bool DrawPropertiesUI(Material material)
        {
            if (material == null) return false;
            
            bool changed = false;

            foreach (var categoryEntry in propertyCategories)
            {
                var category = categoryEntry.Value;
                
                // フォールドアウト状態の初期化
                if (!foldoutStates.ContainsKey(category.Name))
                {
                    foldoutStates[category.Name] = false;
                }

                // カテゴリの有効状態を確認
                bool categoryEnabled = category.IsEnabled;
                if (!string.IsNullOrEmpty(category.EnableKey) && material.HasProperty(category.EnableKey))
                {
                    // プロパティキャッシュから値を取得するか、マテリアルから読み込む
                    int value;
                    string cacheKey = category.EnableKey;
                    
                    if (propertyCache.TryGetValue(material, out var materialCache) && 
                        materialCache.TryGetValue(cacheKey, out var cachedValue))
                    {
                        value = (int)cachedValue;
                    }
                    else
                    {
                        value = material.GetInt(category.EnableKey);
                        if (!propertyCache.ContainsKey(material))
                        {
                            propertyCache[material] = new Dictionary<string, object>();
                        }
                        propertyCache[material][cacheKey] = value;
                    }
                    
                    categoryEnabled = value == 1;
                }

                // カテゴリのフォールドアウト
                EditorGUILayout.BeginVertical(GUI.skin.box);
                
                EditorGUILayout.BeginHorizontal();
                bool prevFoldout = foldoutStates[category.Name];
                bool newFoldout = EditorGUILayout.Foldout(prevFoldout, category.Name, true);
                
                if (prevFoldout != newFoldout)
                {
                    foldoutStates[category.Name] = newFoldout;
                    changed = true;
                }
                
                // カテゴリの有効/無効の表示
                if (!string.IsNullOrEmpty(category.EnableKey))
                {
                    GUIContent labelContent = new GUIContent(categoryEnabled ? "有効" : "無効", 
                        categoryEnabled ? "このカテゴリのプロパティは有効です。" : "このカテゴリのプロパティは無効です。");
                    
                    GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
                    labelStyle.normal.textColor = categoryEnabled ? Color.green : Color.gray;
                    
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(labelContent, labelStyle);
                }
                
                EditorGUILayout.EndHorizontal();

                // カテゴリの内容
                if (foldoutStates[category.Name])
                {
                    EditorGUI.indentLevel++;
                    
                    // カテゴリが無効な場合は通知
                    if (!categoryEnabled && !string.IsNullOrEmpty(category.EnableKey))
                    {
                        EditorGUILayout.HelpBox($"このマテリアルでは{category.Name}が無効化されています。必要に応じて有効にするプロパティをコピーしてください。", MessageType.Info);
                    }
                    
                    // プロパティリスト
                    foreach (var property in category.Properties)
                    {
                        bool hasProperty = material.HasProperty(property.Key);
                        EditorGUI.BeginDisabledGroup(!hasProperty);
                        
                        bool prevChecked = property.IsChecked;
                        bool newChecked = EditorGUILayout.ToggleLeft(
                            new GUIContent(property.DisplayName, $"プロパティ: {property.Key}"), 
                            prevChecked);
                            
                        if (prevChecked != newChecked)
                        {
                            property.IsChecked = newChecked;
                            changed = true;
                        }
                        
                        EditorGUI.EndDisabledGroup();
                    }
                    
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            
            return changed;
        }

        /// <summary>
        /// 選択されたマテリアルをバックアップする
        /// </summary>
        /// <param name="materials">バックアップするマテリアルのリスト</param>
        /// <returns>バックアップディレクトリのパス</returns>
        public string BackupMaterials(List<Material> materials)
        {
            if (materials == null || materials.Count == 0)
            {
                LilToonClonerUtils.LogWarning("バックアップするマテリアルがありません。");
                return null;
            }

            try
            {
                string backupDir = LilToonClonerUtils.EnsureBackupDirectoryExists(BackupBasePath);
                
                foreach (var material in materials)
                {
                    if (material == null) continue;
                    
                    string assetPath = AssetDatabase.GetAssetPath(material);
                    if (string.IsNullOrEmpty(assetPath)) continue;
                    
                    string fileName = Path.GetFileName(assetPath);
                    string backupPath = Path.Combine(backupDir, fileName);
                    
                    // マテリアルをコピー
                    AssetDatabase.CopyAsset(assetPath, backupPath);
                }
                
                AssetDatabase.Refresh();
                LilToonClonerUtils.LogInfo($"{materials.Count}個のマテリアルをバックアップしました: {backupDir}");
                return backupDir;
            }
            catch (Exception ex)
            {
                LilToonClonerUtils.LogError($"マテリアルのバックアップ中にエラーが発生しました: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 選択されたプロパティをコピーする
        /// </summary>
        /// <param name="sourceMaterial">コピー元マテリアル</param>
        /// <param name="targetMaterials">コピー先マテリアルのリスト</param>
        /// <returns>処理されたマテリアルの数</returns>
        public int CopySelectedProperties(Material sourceMaterial, List<Material> targetMaterials)
        {
            if (sourceMaterial == null)
            {
                LilToonClonerUtils.LogError("コピー元マテリアルが指定されていません。");
                return 0;
            }

            if (targetMaterials == null || targetMaterials.Count == 0)
            {
                LilToonClonerUtils.LogError("コピー先マテリアルが指定されていません。");
                return 0;
            }

            int processedCount = 0;
            
            try
            {
                // 選択されたプロパティを収集
                List<PropertyInfo> selectedProperties = new List<PropertyInfo>();
                foreach (var category in propertyCategories.Values)
                {
                    foreach (var property in category.Properties)
                    {
                        if (property.IsChecked && sourceMaterial.HasProperty(property.Key))
                        {
                            selectedProperties.Add(property);
                        }
                    }
                }

                if (selectedProperties.Count == 0)
                {
                    LilToonClonerUtils.LogWarning("コピーするプロパティが選択されていません。");
                    return 0;
                }

                // 処理の進捗状況を表示
                int totalMaterials = targetMaterials.Count;
                int currentMaterial = 0;
                
                // 処理するプロパティを一度キャッシュする
                Dictionary<string, object> sourceValues = CacheSourceMaterialProperties(sourceMaterial, selectedProperties);

                // 並列処理または逐次処理でマテリアルを更新
                if (useParallelProcessing && totalMaterials > 5)  // 少数の場合は並列処理のオーバーヘッドを避ける
                {
                    Parallel.ForEach(targetMaterials, material => {
                        if (CopyPropertiesToMaterial(sourceMaterial, material, selectedProperties, sourceValues))
                        {
                            System.Threading.Interlocked.Increment(ref processedCount);
                        }
                        System.Threading.Interlocked.Increment(ref currentMaterial);
                        float progress = (float)currentMaterial / totalMaterials;
                        EditorUtility.DisplayProgressBar("マテリアルの更新", 
                            $"{currentMaterial}/{totalMaterials} マテリアルを処理中...", progress);
                    });
                }
                else
                {
                    foreach (var material in targetMaterials)
                    {
                        if (CopyPropertiesToMaterial(sourceMaterial, material, selectedProperties, sourceValues))
                        {
                            processedCount++;
                        }
                        currentMaterial++;
                        float progress = (float)currentMaterial / totalMaterials;
                        EditorUtility.DisplayProgressBar("マテリアルの更新", 
                            $"{currentMaterial}/{totalMaterials} マテリアルを処理中...", progress);
                    }
                }
                
                EditorUtility.ClearProgressBar();

                // アセットを保存
                AssetDatabase.SaveAssets();
                LilToonClonerUtils.LogInfo($"{processedCount}個のマテリアルにプロパティをコピーしました。");
                return processedCount;
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                LilToonClonerUtils.LogError($"プロパティのコピー中にエラーが発生しました: {ex.Message}");
                return processedCount;
            }
        }

        /// <summary>
        /// ソースマテリアルからプロパティ値をキャッシュする
        /// </summary>
        private Dictionary<string, object> CacheSourceMaterialProperties(Material source, List<PropertyInfo> properties)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            
            foreach (var property in properties)
            {
                try
                {
                    switch (property.Type)
                    {
                        case MaterialPropertyType.Int:
                            values[property.Key] = source.GetInt(property.Key);
                            break;
                        case MaterialPropertyType.Float:
                            values[property.Key] = source.GetFloat(property.Key);
                            break;
                        case MaterialPropertyType.Color:
                            values[property.Key] = source.GetColor(property.Key);
                            break;
                        case MaterialPropertyType.Vector:
                            values[property.Key] = source.GetVector(property.Key);
                            break;
                        case MaterialPropertyType.Texture:
                            values[property.Key] = source.GetTexture(property.Key);
                            if (source.HasProperty(property.Key + "_ST"))
                            {
                                values[property.Key + "_ST"] = source.GetVector(property.Key + "_ST");
                            }
                            break;
                        case MaterialPropertyType.Cube:
                            values[property.Key] = source.GetTexture(property.Key);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"プロパティ '{property.Key}' のキャッシュ中にエラー: {ex.Message}");
                }
            }
            
            return values;
        }

        /// <summary>
        /// 指定したマテリアルにプロパティをコピーする
        /// </summary>
        /// <param name="source">コピー元マテリアル</param>
        /// <param name="target">コピー先マテリアル</param>
        /// <param name="selectedProperties">コピーするプロパティリスト</param>
        /// <param name="sourceValues">キャッシュされたソース値</param>
        /// <returns>コピーが成功したかどうか</returns>
        private bool CopyPropertiesToMaterial(Material source, Material target, List<PropertyInfo> selectedProperties, 
            Dictionary<string, object> sourceValues)
        {
            if (source == null || target == null) return false;

            // マテリアルがアセットでない場合は保存できない
            string targetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(targetPath)) return false;

            // 同じマテリアルの場合はスキップ
            if (source == target) return false;

            // 各プロパティをコピー
            foreach (var property in selectedProperties)
            {
                if (!target.HasProperty(property.Key)) continue;

                try
                {
                    if (!sourceValues.TryGetValue(property.Key, out object value))
                        continue;

                    switch (property.Type)
                    {
                        case MaterialPropertyType.Int:
                            target.SetInt(property.Key, (int)value);
                            break;
                        case MaterialPropertyType.Float:
                            target.SetFloat(property.Key, (float)value);
                            break;
                        case MaterialPropertyType.Color:
                            target.SetColor(property.Key, (Color)value);
                            break;
                        case MaterialPropertyType.Vector:
                            target.SetVector(property.Key, (Vector4)value);
                            break;
                        case MaterialPropertyType.Texture:
                            target.SetTexture(property.Key, (Texture)value);
                            if (target.HasProperty(property.Key + "_ST") && 
                                sourceValues.TryGetValue(property.Key + "_ST", out object stValue))
                            {
                                target.SetVector(property.Key + "_ST", (Vector4)stValue);
                            }
                            break;
                        case MaterialPropertyType.Cube:
                            target.SetTexture(property.Key, (Texture)value);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"プロパティ '{property.Key}' のコピー中にエラー: {ex.Message}");
                }
            }

            EditorUtility.SetDirty(target);
            return true;
        }
    }
}