using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 将端口默认 <see cref="EdgeConnector{TEdge}"/> 替换为使用自定义 <see cref="IEdgeConnectorListener"/> 的版本。
    /// </summary>
    internal static class BTPortEdgeConnectorUtility
    {
        private static readonly FieldInfo ManipulatorsField =
            typeof(VisualElement).GetField("m_Manipulators", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void ReplaceWithListener(Port port, IEdgeConnectorListener listener)
        {
            if (port == null || listener == null)
                return;

            RemoveEdgeConnectors(port);
            port.AddManipulator(new EdgeConnector<Edge>(listener));
        }

        private static void RemoveEdgeConnectors(Port port)
        {
            var raw = ManipulatorsField?.GetValue(port);
            if (raw is not IEnumerable enumerable)
                return;

            var toRemove = new List<IManipulator>();
            foreach (var item in enumerable)
            {
                if (item is not IManipulator m)
                    continue;
                var t = m.GetType();
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(EdgeConnector<>))
                    toRemove.Add(m);
            }

            foreach (var m in toRemove)
                port.RemoveManipulator(m);
        }
    }
}
