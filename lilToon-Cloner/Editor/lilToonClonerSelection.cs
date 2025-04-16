using System.Collections.Generic;
using UnityEngine;

namespace LilToonCloner
{
    /// <summary>
    /// lilToon-Clonerで選択対象となるマテリアルの選択状態を管理するクラス
    /// </summary>
    public class LilToonClonerSelection
    {
        // 選択状態を保持するディクショナリ - インデックスをキーとして選択状態を保存
        private Dictionary<int, bool> selectionStates = new Dictionary<int, bool>();

        /// <summary>
        /// 指定されたインデックスのアイテムの選択状態を設定する
        /// </summary>
        /// <param name="index">対象アイテムのインデックス</param>
        /// <param name="isSelected">選択状態 (true=選択済み、false=未選択)</param>
        public void SetSelection(int index, bool isSelected)
        {
            selectionStates[index] = isSelected;
        }

        /// <summary>
        /// 指定されたインデックスのアイテムが選択されているかどうかを確認する
        /// </summary>
        /// <param name="index">確認するアイテムのインデックス</param>
        /// <returns>選択状態 (true=選択済み、false=未選択)</returns>
        public bool IsSelected(int index)
        {
            if (selectionStates.TryGetValue(index, out bool isSelected))
            {
                return isSelected;
            }

            // デフォルトでは未選択
            return false;
        }

        /// <summary>
        /// すべてのアイテムを選択状態にする
        /// </summary>
        /// <param name="count">アイテムの総数</param>
        public void SelectAll(int count = int.MaxValue)
        {
            for (int i = 0; i < count; i++)
            {
                selectionStates[i] = true;
            }
        }

        /// <summary>
        /// すべてのアイテムの選択を解除する
        /// </summary>
        public void DeselectAll()
        {
            foreach (int key in selectionStates.Keys)
            {
                selectionStates[key] = false;
            }
        }

        /// <summary>
        /// すべてのアイテムの選択状態を反転する
        /// </summary>
        public void InvertSelection()
        {
            List<int> keys = new List<int>(selectionStates.Keys);
            foreach (int key in keys)
            {
                selectionStates[key] = !selectionStates[key];
            }
        }

        /// <summary>
        /// 選択されているアイテムの数を取得する
        /// </summary>
        /// <returns>選択されているアイテムの数</returns>
        public int GetSelectionCount()
        {
            int count = 0;
            foreach (bool isSelected in selectionStates.Values)
            {
                if (isSelected)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 選択状態をすべてクリアする
        /// </summary>
        public void ClearSelection()
        {
            selectionStates.Clear();
        }

        /// <summary>
        /// 選択されているインデックスのリストを取得する
        /// </summary>
        /// <returns>選択されているアイテムのインデックスのリスト</returns>
        public List<int> GetSelectedIndices()
        {
            List<int> selectedIndices = new List<int>();
            foreach (var kvp in selectionStates)
            {
                if (kvp.Value)
                {
                    selectedIndices.Add(kvp.Key);
                }
            }
            return selectedIndices;
        }
    }
}