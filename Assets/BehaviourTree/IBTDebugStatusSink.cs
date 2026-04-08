namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 接收单次树根 <see cref="BehaviourTree.Tick"/> 内各节点的返回状态（供编辑器叠加显示）。
    /// </summary>
    public interface IBTDebugStatusSink
    {
        /// <summary>新一轮根 Tick 开始前清空上一帧记录。</summary>
        void BeginTickFrame();

        void RecordNodeStatus(string nodeId, BTStatus status);
    }
}
