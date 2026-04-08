using System;
using System.Collections.Generic;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 黑板：在节点之间共享数据（通常每个 Agent / 每棵树一份实例）。
    /// </summary>
    public class BTContext
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        /// <summary>可选；非 null 时每个节点 <see cref="BTNode.Tick"/> 结束会汇报状态。</summary>
        public IBTDebugStatusSink DebugStatusSink { get; set; }

        public bool TryGet<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var o) && o is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public T Get<T>(string key)
        {
            if (TryGet<T>(key, out var v))
                return v;
            throw new KeyNotFoundException($"BTContext missing key '{key}' of type {typeof(T).Name}.");
        }

        public T GetOrDefault<T>(string key, T defaultValue = default)
        {
            return TryGet<T>(key, out var v) ? v : defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            _data[key] = value;
        }

        public bool Remove(string key) => _data.Remove(key);

        public void Clear() => _data.Clear();
    }
}
