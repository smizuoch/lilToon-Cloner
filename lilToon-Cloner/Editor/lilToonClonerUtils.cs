using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace LilToonCloner
{
    /// <summary>
    /// lilToon-Cloner用のユーティリティクラス
    /// GUI初期化、エラーハンドリング、共通機能を提供
    /// </summary>
    public static class LilToonClonerUtils
    {
        /// <summary>
        /// GUIスタイルのボックスを初期化する
        /// </summary>
        /// <param name="border">ボーダーのサイズ</param>
        /// <param name="margin">マージンのサイズ</param>
        /// <param name="padding">パディングのサイズ</param>
        /// <returns>初期化されたGUIStyle</returns>
        public static GUIStyle InitializeBox(int border, int margin, int padding)
        {
            return new GUIStyle
            {
                border = new RectOffset(border, border, border, border),
                margin = new RectOffset(margin, margin, margin, margin),
                padding = new RectOffset(padding, padding, padding, padding),
                overflow = new RectOffset(0, 0, 0, 0)
            };
        }

        /// <summary>
        /// GUIStyleの背景テクスチャをロードする
        /// </summary>
        /// <param name="style">背景を設定するGUIStyle</param>
        /// <param name="path">テクスチャのパス</param>
        /// <returns>ロードに成功したかどうか</returns>
        public static bool TryLoadBackgroundTexture(GUIStyle style, string path)
        {
            try
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    style.normal.background = texture;
                    return true;
                }
                else
                {
                    Debug.LogWarning($"テクスチャのロードに失敗しました: {path}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"テクスチャのロード中にエラーが発生しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// バックアップディレクトリを作成・確認する
        /// </summary>
        /// <param name="basePath">ベースパス</param>
        /// <returns>作成されたバックアップディレクトリのパス</returns>
        public static string EnsureBackupDirectoryExists(string basePath = "Assets/Backups")
        {
            string backupDir = basePath;
            
            // タイムスタンプ付きのディレクトリ名を生成
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backupDir = Path.Combine(backupDir, $"lilToonCloner_{timestamp}");
            
            // ディレクトリが存在しない場合は作成
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
                AssetDatabase.Refresh();
            }
            
            return backupDir;
        }

        /// <summary>
        /// マテリアルが有効かつlilToonシェーダーを使用しているかどうかを確認する
        /// </summary>
        /// <param name="material">確認するマテリアル</param>
        /// <returns>lilToonマテリアルかどうか</returns>
        public static bool IsValidLilToonMaterial(Material material)
        {
            if (material == null) return false;
            if (material.shader == null) return false;
            
            return material.shader.name.Contains("lilToon");
        }

        /// <summary>
        /// エラーログを出力してダイアログを表示する
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        /// <param name="showDialog">ダイアログを表示するかどうか</param>
        public static void LogError(string message, bool showDialog = true)
        {
            Debug.LogError($"[lilToon-Cloner] エラー: {message}");
            
            if (showDialog)
            {
                EditorUtility.DisplayDialog("エラー", message, "OK");
            }
        }

        /// <summary>
        /// 警告ログを出力する
        /// </summary>
        /// <param name="message">警告メッセージ</param>
        /// <param name="showDialog">ダイアログを表示するかどうか</param>
        public static void LogWarning(string message, bool showDialog = false)
        {
            Debug.LogWarning($"[lilToon-Cloner] 警告: {message}");
            
            if (showDialog)
            {
                EditorUtility.DisplayDialog("警告", message, "OK");
            }
        }

        /// <summary>
        /// 情報ログを出力する
        /// </summary>
        /// <param name="message">情報メッセージ</param>
        public static void LogInfo(string message)
        {
            Debug.Log($"[lilToon-Cloner] 情報: {message}");
        }

        /// <summary>
        /// UI上に区切り線を描画する
        /// </summary>
        public static void DrawSeparator()
        {
            EditorGUILayout.Space();
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space();
        }
    }
}