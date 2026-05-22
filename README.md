# Shibafu BehaviourTree

这是一个 Unity 行为树运行与编辑工具。行为树定义保存在 `BTDefinitionScriptableObject` 资产中，图节点的 `data` 字段使用 JSON 保存，运行时由 `BehaviourTreeRunner` 加载并执行。

## ObjectBindings 持久化逻辑

行为树节点如果需要引用 Unity 对象，例如 `BTMoveToTargetAction` 的 `subject` 和 `target`，图编辑器不会把对象直接写进 JSON。JSON 中只保存一个形如 `btref:<bindingId>` 的字符串，真正的 Unity 对象引用登记在 `BTDefinitionScriptableObject.ObjectBindings` 里。

这样做的原因是：行为树定义是一个 `.asset`，而场景对象属于 `.unity` 场景。Unity 资产不能可靠地直接持久化场景对象引用，尤其是重启 Unity 后，`ObjectBindings` 里直接序列化的 `target` 很容易变成 `None`。因此现在的绑定条目采用“对象引用 + 可恢复描述信息”的双轨方案。

每个 `BTObjectBindingEntry` 会保存这些字段：

- `bindingId`：JSON 中 `btref:` 后面的稳定 ID，用来把节点字段和绑定表条目关联起来。
- `target`：Unity 当前会话中的真实对象引用。它用于编辑器预览和运行时快速访问，但不再作为唯一持久化依据。
- `targetGlobalObjectId`：编辑器下由 `UnityEditor.GlobalObjectId` 生成的对象 ID。Unity 能解析时会优先用它恢复引用。
- `targetScenePath`：目标对象所在场景的路径，用于限定运行时按层级搜索的场景。
- `targetHierarchyPath`：目标对象从场景根开始的层级路径，例如 `Root/Enemy/TargetPoint`。
- `targetTypeName`：目标对象的程序集限定类型名，用于恢复后把 `GameObject`、`Transform` 或组件类型转换回原始字段需要的类型。

## 保存流程

当图编辑器里的 ObjectField 选择了一个 Unity 对象时，会调用 `EditorAssignObjectBinding`：

1. 如果当前 JSON 字段已经是 `btref:<id>`，复用原来的 `bindingId`；否则生成一个新的 GUID。
2. 将节点 JSON 中对应字段写成 `btref:<bindingId>`。
3. 在 `ObjectBindings` 中新增或更新同 ID 的条目。
4. 保存当前 `target` 引用。
5. 调用 `EditorUpdateObjectBindingMetadata` 记录 `targetGlobalObjectId`、`targetTypeName`。
6. 如果目标是场景里的 `GameObject` 或 `Component`，额外记录 `targetScenePath` 和 `targetHierarchyPath`。

`OnValidate` 也会调用 `EditorRefreshObjectBindingMetadata`。这意味着只要 `target` 当前还没有丢失，Unity 重新导入或 Inspector 校验时会自动补齐/刷新这些恢复用字段。

## 恢复流程

运行时加载行为树时，`BehaviourTreeRunner` 会创建 `BTDefinitionLoadContext`，并调用 `EnsureBindingResolverOnContext`。这个方法给上下文挂上 `TryGetBoundObjectById`，之后 `BTNodeReferenceBinder` 解析节点字段时遇到 `btref:<id>`，会回到 `ObjectBindings` 查找真实对象。

解析某个绑定时使用 `ResolveObjectBindingTarget`，顺序如下：

1. 如果 `target` 仍然有效，直接返回它。
2. 在 Unity 编辑器中，如果 `targetGlobalObjectId` 可解析，就用 `GlobalObjectIdentifiersToObjectsSlow` 找回对象。
3. 如果 `GlobalObjectId` 不可用，或者是在运行时环境，则使用 `targetHierarchyPath` 按场景层级查找。
4. 查找层级时会优先使用 `BehaviourTreeRunner` 配置到 `BTDefinitionLoadContext.UnityObjectResolveRoot` 的根节点；找不到再扫描已加载场景。
5. 如果保存了 `targetScenePath`，会优先限定在该场景中查找。
6. 找到 Transform 后，根据 `targetTypeName` 转回需要的类型：`GameObject` 返回 `gameObject`，`Transform` 返回 `transform`，组件类型则执行 `GetComponent(type)`。

编辑器侧还有 `OnEnable` 调用 `EditorResolveMissingObjectBindingTargets`。因此如果重启 Unity 后 `target` 显示为 `None`，但恢复字段已经保存过，打开/加载资产时会尝试自动把 `target` 填回去。

## 和层级路径解析的关系

项目里原本就支持直接在 JSON 中写层级路径，由 `BTUnityObjectIdResolve` 使用 `Transform.Find` 解析。`ObjectBindings` 的层级路径恢复和它目标不同：

- JSON 层级路径是手写/直接保存到节点 `data` 的引用方式。
- `ObjectBindings` 层级路径是 `btref:` 绑定的兜底恢复信息，主要用于解决 `.asset` 中场景对象引用重启后变 `None` 的问题。

推荐在图编辑器中通过 ObjectField 选择对象，让系统写入 `btref:` 和绑定表；只有需要完全手写 JSON 时，再使用直接层级路径。

## 使用注意

新增持久化字段后，已有绑定需要在 Unity 重新编译后重新保存一次行为树定义资产，才能把 `targetGlobalObjectId`、`targetScenePath`、`targetHierarchyPath` 和 `targetTypeName` 写入 `.asset`。

如果某个绑定在保存这些新字段之前已经丢成了 `None`，系统没有足够信息自动恢复，需要重新在图编辑器 ObjectField 里选择目标对象一次。

层级路径恢复依赖对象名称和父子结构。移动、重命名对象，或者场景中存在完全相同的层级路径时，恢复结果可能不符合预期。稳定引用优先依赖编辑器 `GlobalObjectId`，运行时兜底才依赖层级路径。

