# 需求文档：锄地一条龙（AutoHoeingOneDragon）原生C#独立任务

## 简介

将现有的BetterGenshinImpact JS脚本"锄地一条龙"（v2.7.2）转写为BetterGI原生C#独立任务（ISoloTask）。该任务实现原神自动化锄地功能，包括路线智能选择与优化、多路径组分配、并发路线执行与拾取、异常状态检测与恢复、CD时间管理和数据持久化等核心能力。C#版本应直接调用BetterGI内部API（PathExecutor、RecognitionObject、图像识别等），而非通过JS桥接层。

## 术语表

- **AutoHoeingTask**：锄地一条龙C#独立任务主类，实现ISoloTask接口
- **RouteInfo**：路线信息数据模型，包含路线文件路径、预计用时、怪物信息、标签、效率指数等
- **MonsterInfo**：怪物信息数据模型，包含怪物名称、类型（普通/精英）、摩拉倍率、标签、掉落物品
- **RouteSelector**：路线选择与优化引擎，负责效率计算、迭代优化、贪心逆筛
- **RouteGroupAssigner**：路线分组分配器，将已选路线按标签分配到10个路径组
- **RouteExecutionEngine**：路线执行引擎，负责单条路线的执行与并发监控
- **TemplatePickupService**：模板匹配拾取服务，识别F图标并进行物品模板匹配与拾取
- **AnomalyDetector**：异常状态检测器，检测冻结、白芙、复苏、烹饪界面等异常
- **DumperService**：泥头车服务，在接近战斗点时提前切人放E技能
- **CdManager**：CD时间管理器，管理精英（24h）和小怪（12h）的刷新冷却
- **RunRecord**：运行记录数据模型，存储最近7次运行时长用于自我优化
- **BlacklistManager**：黑名单管理器，管理背包满时OCR识别并拉黑的物品
- **AutoHoeingConfig**：锄地一条龙配置类，存储所有用户可配置参数
- **PathExecutor**：BetterGI已有的地图追踪执行器
- **PathingTask**：BetterGI已有的路线数据模型
- **效率指数E1**：精英路线效率 = (权衡因数 × 精英摩拉收益 - 用时) / 精英数量
- **效率指数E2**：小怪路线效率 = (权衡因数 × 小怪摩拉收益 - 用时) / 小怪数量

## 需求

### 需求1：任务框架与生命周期

**用户故事：** 作为BetterGI用户，我希望锄地一条龙作为原生独立任务运行，以获得更好的性能和更紧密的系统集成。

#### 验收标准

1. THE AutoHoeingTask SHALL 实现ISoloTask接口，提供Name属性（返回"锄地一条龙"）和Start(CancellationToken ct)方法
2. WHEN Start方法被调用时，THE AutoHoeingTask SHALL 按以下顺序执行：加载配置 → 路线预处理 → 路线选择优化 → 路线分组 → 队伍校验 → 逐组执行路线
3. WHEN CancellationToken被取消时，THE AutoHoeingTask SHALL 在当前路线执行完毕后安全停止，释放所有资源
4. IF 任务执行过程中发生未处理异常，THEN THE AutoHoeingTask SHALL 记录异常日志并安全终止任务

### 需求2：配置管理

**用户故事：** 作为用户，我希望通过WPF配置界面管理锄地参数，以便灵活调整执行策略。

#### 验收标准

1. THE AutoHoeingConfig SHALL 继承ObservableObject，包含以下配置分组：执行模式（运行锄地路线、调试路线分配、强制刷新记录、仅指定怪物模式）、路径组选择（1-10）、配队名称、排序模式（原文件顺序、效率降序、高收益优先）、拾取模式（模板匹配拾取狗粮和怪物材料、模板匹配仅拾取狗粮、BGI原版拾取、不拾取）
2. THE AutoHoeingConfig SHALL 包含路线选择参数：账户名称、10个路径组的标签配置、摩拉/耗时权衡因数（默认0.25）、好奇系数（默认0）、忽略比例（默认100）、目标精英数量（默认400）、目标小怪数量（默认2000）、优先关键词、排除关键词
3. THE AutoHoeingConfig SHALL 包含执行参数：泥头车角色编号列表、料理名称列表、不运行时段、识别间隔（默认100ms）、拾取后延时（默认50ms）、滚动后延时（默认32ms）、单次滚动周期（默认1000ms）
4. THE AutoHoeingConfig SHALL 包含高级选项：禁用自我优化、启用坐标检查、跳过校验、禁用异步操作、输出怪物数量日志、仅使用路线相关怪物材料识别、禁用二次校验
5. WHEN 用户选择路径组一时，THE AutoHoeingConfig SHALL 将第二部分路线选择配置保存到以账户名称命名的JSON文件中
6. WHEN 用户选择非路径组一时，THE AutoHoeingConfig SHALL 从对应账户名称的JSON文件中读取第二部分路线选择配置
7. THE AutoHoeingConfig SHALL 被序列化存储在BetterGI的AllConfig体系中，随应用启动自动加载

### 需求3：路线预处理与信息提取

**用户故事：** 作为用户，我希望系统自动解析所有路线文件并提取关键信息，以便进行智能路线选择。

#### 验收标准

1. WHEN 任务启动时，THE RouteInfo SHALL 扫描pathing目录下所有JSON路线文件，并反序列化每个文件的info.description字段
2. THE RouteInfo SHALL 通过正则表达式从description中提取"预计用时X秒"和"包含以下怪物：N只怪物名、M只怪物名。"信息
3. WHEN description中未包含预计用时信息时，THE RouteInfo SHALL 使用默认值60秒
4. THE RouteInfo SHALL 读取monsterInfo.json怪物信息表，将每条路线的怪物名称映射为普通怪数量(m)、精英怪数量(e)、普通怪摩拉收益(mora_m = 数量 × 40.5 × moraRate)、精英怪摩拉收益(mora_e = 数量 × 200 × moraRate)
5. WHEN 怪物的moraRate大于1时，THE RouteInfo SHALL 为该路线添加"高收益"标签；若该怪物为精英类型，还应添加"精英高收益"标签
6. WHEN 路线的小怪数量/精英数量比值大于等于ignoreRate配置值时，且路线不含"精英高收益""高危""传奇"标签，THE RouteInfo SHALL 将该路线的精英数量和精英摩拉收益置为0
7. THE RouteInfo SHALL 从路线文件的info.tags字段和怪物信息的tags字段收集标签，并通过路径名和description文本匹配用户配置的所有标签关键词，合并去重后存储

### 需求4：自我优化（历史运行时长调整）

**用户故事：** 作为用户，我希望系统根据历史运行记录自动调整路线预期用时，以提高路线选择的准确性。

#### 验收标准

1. WHILE 自我优化功能未被禁用时，THE RouteSelector SHALL 读取每条路线最近7次运行时长记录
2. WHEN 运行记录不足7条时，THE RouteSelector SHALL 使用"默认用时 × (1 - 好奇系数)"填充缺失记录
3. THE RouteSelector SHALL 对7条记录执行削峰填谷处理：去除一个最大值和一个最小值，对剩余记录取算术平均值作为调整后的预期用时
4. WHEN 自我优化功能被禁用时，THE RouteSelector SHALL 完全使用路线description中的原始预计用时

### 需求5：路线选择与优化算法

**用户故事：** 作为用户，我希望系统自动计算最优路线组合，在目标怪物数量约束下最大化效率。

#### 验收标准

1. THE RouteSelector SHALL 为每条可用路线计算精英效率指数E1 = (权衡因数 × mora_e - t) / e，以及小怪效率指数E2 = (权衡因数 × mora_m - t) / m
2. WHEN 路线精英数量为0时，THE RouteSelector SHALL 将E1设为所有路线E1最小值减1；WHEN 路线小怪数量为0时，THE RouteSelector SHALL 将E2设为所有路线E2最小值减1
3. WHEN 路线被标记为优先(prioritized)时，THE RouteSelector SHALL 将该路线的E1和E2分别增加(maxE1 - minE1 + 2)和(maxE2 - minE2 + 2)
4. THE RouteSelector SHALL 执行两轮选择：第一轮按E1降序选择精英路线直到精英总数达到目标值；第二轮按E2降序从未选路线中补选小怪路线直到小怪总数达到目标值
5. THE RouteSelector SHALL 通过迭代调整精英目标门槛（最多100次迭代），使精英和小怪总数同时落在目标区间内
6. THE RouteSelector SHALL 执行贪心逆筛：按E1升序遍历已选的非优先、非精英高收益路线，若移除后精英和小怪总数仍满足目标，则移除该路线
7. WHEN 路线选择完成后，THE RouteSelector SHALL 按用户配置的排序模式（原文件顺序、效率降序、高收益优先）对已选路线排序

### 需求6：路线标记与过滤

**用户故事：** 作为用户，我希望通过标签和关键词灵活控制哪些路线参与选择。

#### 验收标准

1. THE RouteSelector SHALL 将路径组一的排除标签中仅属于组一（不与其他组共享）的标签视为互斥标签：路线包含任一互斥标签时标记为不可用(available=false)
2. WHEN 路线的文件路径、已有标签或所含怪物名命中任一排除关键词时，THE RouteSelector SHALL 将该路线标记为不可用
3. WHEN 路线的文件路径、已有标签或所含怪物名命中任一优先关键词时，THE RouteSelector SHALL 将该路线标记为优先(prioritized=true)
4. WHEN 拾取模式不包含"模板匹配"时，THE RouteSelector SHALL 自动将"沙暴"添加到排除关键词列表

### 需求7：路线分组分配

**用户故事：** 作为用户，我希望已选路线按标签自动分配到不同路径组，以便使用不同队伍分组执行。

#### 验收标准

1. THE RouteGroupAssigner SHALL 仅处理selected为true的路线
2. WHEN 路线不含路径组一任何标签时，THE RouteGroupAssigner SHALL 将该路线分配到路径组1
3. WHEN 路线含有路径组一的标签时，THE RouteGroupAssigner SHALL 按路径组2至路径组10的顺序匹配标签，命中即分配到对应组
4. WHEN 用户选择"调试路线分配"模式时，THE RouteGroupAssigner SHALL 输出每组的路线条数、精英数、小怪数、预计收益和预计用时的汇总信息

### 需求8：仅指定怪物模式

**用户故事：** 作为用户，我希望能指定特定怪物名称，系统自动筛选包含这些怪物的路线执行。

#### 验收标准

1. WHEN 执行模式为"仅指定怪物模式"时，THE AutoHoeingTask SHALL 解析用户填写的目标怪物字符串（中文逗号分隔）
2. THE AutoHoeingTask SHALL 对每条路线在文件路径和description中全文匹配任一目标怪物关键字，命中则标记为selected并分配到路径组1
3. THE AutoHoeingTask SHALL 跳过标签分组和效率优化流程，直接进入路线执行阶段

### 需求9：路线执行引擎

**用户故事：** 作为用户，我希望系统高效执行每条路线，同时并发处理拾取和异常检测。

#### 验收标准

1. WHEN 执行一条路线时，THE RouteExecutionEngine SHALL 调用BetterGI的PathExecutor执行地图追踪
2. THE RouteExecutionEngine SHALL 并发运行以下子任务：主路线执行、模板匹配拾取、异常状态检测、背包满黑名单检测；WHEN 泥头车角色列表非空时，还应并发运行泥头车任务
3. WHEN 主路线执行完成时，THE RouteExecutionEngine SHALL 设置running标志为false，所有并发子任务应在检测到该标志后自行终止
4. WHEN 上一条路线检测到白芙状态时，THE RouteExecutionEngine SHALL 在下一条路线开始前执行强制黑芙切换路线
5. WHEN 料理配置非空且距上次使用超过300秒时，THE RouteExecutionEngine SHALL 在路线执行前自动打开背包使用配置的料理

### 需求10：模板匹配拾取系统

**用户故事：** 作为用户，我希望系统在路线执行过程中自动识别并拾取掉落物品。

#### 验收标准

1. WHILE 路线正在执行时，THE TemplatePickupService SHALL 持续截取游戏画面，在指定区域(1102, 335, 34, 400)识别F图标
2. WHEN F图标被识别到时，THE TemplatePickupService SHALL 对F图标上方区域进行物品模板匹配，从targetItems列表中找到匹配的物品名称
3. WHEN 物品名称在黑名单中时，THE TemplatePickupService SHALL 跳过该物品不执行拾取
4. WHEN 物品名称不在黑名单中时，THE TemplatePickupService SHALL 发送F键按下事件执行拾取，并记录拾取日志
5. WHEN 连续两次识别到相同物品名称且F图标Y坐标差值小于等于20像素时，THE TemplatePickupService SHALL 跳过本次拾取以避免重复操作
6. WHEN 未识别到F图标且距上次滚动超过200ms时，THE TemplatePickupService SHALL 执行滚轮下翻操作以扩大识别范围
7. WHEN "仅使用路线相关怪物材料识别"选项启用时，THE TemplatePickupService SHALL 仅启用当前路线包含的怪物对应的材料模板和历史拾取过的材料模板

### 需求11：异常状态检测与恢复

**用户故事：** 作为用户，我希望系统自动检测并处理执行过程中的异常状态。

#### 验收标准

1. WHILE 路线正在执行时，THE AnomalyDetector SHALL 每约250毫秒执行一次冻结状态检测（模板匹配"解除冰冻"图标）
2. WHEN 检测到冻结状态时，THE AnomalyDetector SHALL 连续发送3次空格键按下事件以挣脱冻结
3. WHILE 路线正在执行时，THE AnomalyDetector SHALL 每约250毫秒执行一次白芙状态检测（模板匹配"白芙图标"，阈值0.97）
4. WHEN 检测到白芙状态时，THE AnomalyDetector SHALL 设置shouldSwitchFurina标志为true，在当前路线结束后触发形态切换
5. WHILE 路线正在执行时，THE AnomalyDetector SHALL 每约250毫秒执行一次复苏按钮检测（模板匹配"复苏"图标，阈值0.95）
6. WHEN 检测到复苏按钮时，THE AnomalyDetector SHALL 点击复苏按钮并等待500毫秒
7. WHILE 路线正在执行时，THE AnomalyDetector SHALL 每约5000毫秒执行一次烹饪界面检测（模板匹配"烹饪界面"图标，阈值0.95）
8. WHEN 检测到烹饪界面时，THE AnomalyDetector SHALL 发送ESC键按下事件以脱离烹饪界面

### 需求12：泥头车服务

**用户故事：** 作为用户，我希望在接近战斗点时自动提前释放角色E技能，提高战斗效率。

#### 验收标准

1. WHEN 泥头车角色列表非空时，THE DumperService SHALL 在路线执行过程中监控当前位置与下一个战斗点的距离
2. WHEN 当前位置距离战斗点在5-30范围内时，THE DumperService SHALL 按配置的角色编号顺序切换角色并释放E技能
3. THE DumperService SHALL 仅接受编号1-4的角色配置，忽略无效编号

### 需求13：CD时间管理

**用户故事：** 作为用户，我希望系统自动跟踪怪物刷新CD，避免重复执行未刷新的路线。

#### 验收标准

1. THE CdManager SHALL 为每条路线维护上次执行完成的时间戳
2. WHEN 路线包含精英怪时，THE CdManager SHALL 使用24小时作为CD周期
3. WHEN 路线仅包含小怪时，THE CdManager SHALL 使用12小时作为CD周期
4. WHEN 路线的CD未到期时，THE CdManager SHALL 将该路线标记为不可用，不参与路线选择
5. THE CdManager SHALL 将CD数据以JSON格式持久化存储，按账户名称区分
6. WHEN 用户选择"强制刷新所有运行记录"模式时，THE CdManager SHALL 清除所有路线的CD记录

### 需求14：运行记录与数据持久化

**用户故事：** 作为用户，我希望系统保存运行历史数据，用于自我优化和统计分析。

#### 验收标准

1. THE RunRecord SHALL 为每条路线存储最近7次运行时长（秒），新记录入队时最旧记录出队
2. WHEN 路线执行完成时，THE RunRecord SHALL 记录本次实际运行时长
3. WHEN "启用坐标检查"选项开启且路线结尾坐标与预期不符时，THE RunRecord SHALL 不记录本次运行时长（视为异常执行）
4. THE RunRecord SHALL 将运行记录以JSON格式持久化存储，按账户名称区分
5. THE TemplatePickupService SHALL 将每条路线的拾取物品历史记录存储到路线对象中，用于下次执行时优先识别

### 需求15：黑名单管理

**用户故事：** 作为用户，我希望系统在背包满时自动识别并拉黑无法拾取的物品。

#### 验收标准

1. WHILE 路线正在执行时，THE BlacklistManager SHALL 每约1.5秒检测一次"背包已满"提示（模板匹配itemFull图标）
2. WHEN 检测到背包已满时，THE BlacklistManager SHALL 对提示区域(560, 450, 800, 170)执行OCR识别，提取物品名称文本
3. THE BlacklistManager SHALL 将OCR识别文本与所有targetItems的中文名称进行滑动窗口匹配，匹配度超过75%的物品加入黑名单
4. THE BlacklistManager SHALL 将黑名单以JSON格式持久化存储到blacklists目录，按账户名称区分
5. WHEN 任务启动时，THE BlacklistManager SHALL 加载已有的黑名单数据

### 需求16：队伍校验

**用户故事：** 作为用户，我希望系统在执行前检查队伍配置合理性，避免因配队问题导致执行失败。

#### 验收标准

1. WHEN "跳过校验"选项未启用时，THE AutoHoeingTask SHALL 在路线执行前检查当前队伍配置
2. THE AutoHoeingTask SHALL 检查以下条件并输出警告：目标怪物数量配置不合理（精英≤350且小怪≥100）、游戏窗口非1920×1080、队伍包含钟离、未携带芙宁娜或爱可菲、未携带抗打断角色（茜特菈莉/伊涅芙/莱依拉/蓝砚/绮良良/迪希雅/迪奥娜）
3. IF 当前队伍同时包含钟离、芙宁娜、纳西妲和雷电将军（四神队），THEN THE AutoHoeingTask SHALL 终止任务执行并输出错误日志

### 需求17：时间限制

**用户故事：** 作为用户，我希望设置不运行时段，避免在特定时间执行锄地。

#### 验收标准

1. THE AutoHoeingTask SHALL 解析用户配置的不运行时段字符串，支持以下格式：单个小时（如"8"）、连续区间（如"8-11"或"23:11-23:55"）、多项用中文逗号分隔
2. WHEN 当前本地时间处于不运行时段内时，THE AutoHoeingTask SHALL 暂停路线执行并等待
3. WHEN 距离不运行时段开始不足10分钟时，THE AutoHoeingTask SHALL 提前结束当前路线组的执行并等待到限制时段结束

### 需求18：多账户支持

**用户故事：** 作为用户，我希望系统支持多账户运行，不同账户的数据互相隔离。

#### 验收标准

1. THE AutoHoeingTask SHALL 使用账户名称作为数据隔离键，不同账户的CD记录、运行记录、黑名单数据分别存储
2. WHEN 账户名称为空时，THE AutoHoeingTask SHALL 使用"默认账户"作为默认值
3. THE AutoHoeingTask SHALL 支持在不同配置组中使用不同账户名称，实现多账户轮换执行
