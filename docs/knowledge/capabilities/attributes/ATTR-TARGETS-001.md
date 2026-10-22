# ATTR-TARGETS-001 直接特性目标、真实记录与伪特性

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "ATTR-TARGETS-001",
  "title": "直接特性目标、真实记录与伪特性",
  "category": "attributes",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "14"
    },
    {
      "section": "独立可用事实与所需查询（2026-10-14维护者裁决）",
      "path": "docs/language/metadata-availability.md"
    },
    {
      "path": "src/AspectN/TypeSystem/ITypeSystem.cs",
      "section": "AreEqual / IsAssignableTo / HasDirectAttribute"
    },
    {
      "path": "docs/reviews/2026-10-21-d01-direct-attribute-limits.md",
      "section": "维护者裁决与取代范围"
    },
    {
      "path": "docs/superpowers/specs/2026-10-22-configurable-resource-limits-design.md",
      "section": "2—6；仅资源策略与公开入口"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompiledMemberPattern.Match / HasDirectAttribute",
      "conditions": "AttributePatternTests 的 type/call/prop/para/ret/exec/any 与泛型特性中立输入；核心QueryTargetContractTests记录精确特性查询传参；特性首次捕获及多备选用例显式Capture",
      "target": "working-tree",
      "expected_behavior": "只查询直接目标；call 不包含属性位置，exec 对访问器包含关联属性；不可用位置不当作空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261021-003",
        "EV-20261021-007",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper / attr / HasDirectAttribute",
      "conditions": "AttributeMetadataConformanceTests、CustomAttributeRecordTests、MetadataConsistencyTests 的同源/PE fixture；不含 Fody；GenericParameterAttributeTests 的类型/方法泛型形参有、无直接记录及声明类型 DSL 目标对照",
      "target": "working-tree",
      "expected_behavior": "真实 CustomAttribute 记录跨视图保留；编码标志形成的伪特性排除，真实同类型记录仍保留；查询不实例化对象；泛型形参直接 API 读取其自身声明记录，有标记 Matched、已知空 NotMatched，不借用所属类型/方法的特性",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261021-001",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper / attr / HasDirectAttribute",
      "conditions": "AttributeMetadataConformanceTests、CustomAttributeRecordTests、MetadataConsistencyTests 的同源/PE fixture；不含 Fody；GenericParameterAttributeTests 的类型/方法泛型形参有、无直接记录及声明类型 DSL 目标对照",
      "target": "working-tree",
      "expected_behavior": "真实 CustomAttribute 记录跨视图保留；编码标志形成的伪特性排除，真实同类型记录仍保留；查询不实例化对象；泛型形参直接 API 读取其自身声明记录，有标记 Matched、已知空 NotMatched，不借用所属类型/方法的特性",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261021-001",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper / attr / HasDirectAttribute",
      "conditions": "AttributeMetadataConformanceTests、CustomAttributeRecordTests、MetadataConsistencyTests 的同源/PE fixture；不含 Fody；GenericParameterAttributeTests 的类型/方法泛型形参有、无直接记录及声明类型 DSL 目标对照",
      "target": "working-tree",
      "expected_behavior": "真实 CustomAttribute 记录跨视图保留；编码标志形成的伪特性排除，真实同类型记录仍保留；查询不实例化对象；泛型形参直接 API 读取其自身声明记录，有标记 Matched、已知空 NotMatched，不借用所属类型/方法的特性",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261021-001",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S5",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompileMemberExpression",
      "conditions": "AttributeExclusionTests：get/set及callable/property/parameter/return旧位置名；参数含括号/空格、索引/通配写法，并执行支持语法对照",
      "target": "working-tree",
      "expected_behavior": "get/set编译失败且有AN1001；旧位置名有AN4005、不能取得有效模式；短名及getter/setter与attr(call ...)组合可编译",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261022-201"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "Standard位置短名已固定；访问器自身用getter/setter选择器与call位置组合，旧长名只提供迁移诊断",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "get/set编译失败且有AN1001；旧位置名有AN4005、不能取得有效模式；短名及getter/setter与attr(call ...)组合可编译"
      }
    },
    {
      "id": "S6",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / attr(type/call ...)",
      "conditions": "AttributeInheritanceTests：同源/同PE的带标记基类、仅继承的override、显式直接标记的override；原生inherit:true均能看到标记，inherit:false独立证明直接记录",
      "target": "working-tree",
      "expected_behavior": "只继承且无直接记录时NotMatched；基类和显式标记派生类Matched，诊断为空；不把Inherited=true当作直接记录",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261022-201"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言第14节只承诺直接CustomAttribute记录，不执行运行时特性继承规则；继承标志不改变直接记录位置",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "表达式可编译；仅继承的声明类型/override方法在此fixture为NotMatched，不是编译失败或Unknown"
      }
    },
    {
      "id": "S7",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper.MapMethod / attr(type/call ...)",
      "conditions": "AttributeInheritanceTests：同源/同PE的带标记基类、仅继承的override、显式直接标记的override；原生inherit:true均能看到标记，inherit:false独立证明直接记录",
      "target": "working-tree",
      "expected_behavior": "只继承且无直接记录时NotMatched；基类和显式标记派生类Matched，诊断为空；不把Inherited=true当作直接记录",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261022-201"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言第14节只承诺直接CustomAttribute记录，不执行运行时特性继承规则；继承标志不改变直接记录位置",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "表达式可编译；仅继承的声明类型/override方法在此fixture为NotMatched，不是编译失败或Unknown"
      }
    },
    {
      "id": "S8",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / attr(type/call ...)",
      "conditions": "AttributeInheritanceTests：同源/同PE的带标记基类、仅继承的override、显式直接标记的override；原生inherit:true均能看到标记，inherit:false独立证明直接记录",
      "target": "working-tree",
      "expected_behavior": "只继承且无直接记录时NotMatched；基类和显式标记派生类Matched，诊断为空；不把Inherited=true当作直接记录",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261022-201"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言第14节只承诺直接CustomAttribute记录，不执行运行时特性继承规则；继承标志不改变直接记录位置",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "表达式可编译；仅继承的声明类型/override方法在此fixture为NotMatched，不是编译失败或Unknown"
      }
    },
    {
      "id": "S9",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapMethod / attr(type/call ...)",
      "conditions": "AttributeInheritanceTests：同源/同PE的带标记基类、仅继承的override、显式直接标记的override；原生inherit:true均能看到标记，inherit:false独立证明直接记录",
      "target": "working-tree",
      "expected_behavior": "只继承且无直接记录时NotMatched；基类和显式标记派生类Matched，诊断为空；不把Inherited=true当作直接记录",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261022-201"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言第14节只承诺直接CustomAttribute记录，不执行运行时特性继承规则；继承标志不改变直接记录位置",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "表达式可编译；仅继承的声明类型/override方法在此fixture为NotMatched，不是编译失败或Unknown"
      }
    },
    {
      "id": "S10",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapMethod / ReturnAttributes / AreEqual",
      "conditions": "ReadonlyReturnAttributeIdentityTests：真实普通/嵌入标记PE、无错误的同名读取者和多同名依赖，与原生Reflection返回CA和Cecil构造器Scope对照",
      "target": "working-tree",
      "expected_behavior": "恢复原PE真实CA构造器所属的类型/程序集身份，不采用读取者同名类型；与实际标记AreEqual为Matched",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261014-010",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S11",
      "adapter": "reflection",
      "input_mode": "detached",
      "entry_point": "CompiledMemberPattern.Match / attr(type ...)",
      "conditions": "同一已映射候选TypeAttributes已知；原上下文与新ReflectionTypeSystem；分别精确、通配、复合表达式，带标记/已知空两种候选",
      "target": "working-tree",
      "expected_behavior": "优先消费完整已知TypeAttributes，精确、通配、复合在原/新上下文均一致；带标记Matched、已知空NotMatched，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261014-010",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S12",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "CompiledMemberPattern.Match / attr(type ...)",
      "conditions": "TypeAttributeContextTests：TypeAttributes不可用且有/无原生关联；带标记/无标记；具体精确模式；原诊断AN3004控制",
      "target": "working-tree",
      "expected_behavior": "先尝试原生直接查询，有关联可得Matched/NotMatched；无关联Unknown，保留所需事实原诊断",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S13",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "CompiledMemberPattern.Match / attr(type ...)",
      "conditions": "TypeAttributeContextTests：TypeAttributes不可用但原生关联仍在；通配或复合类型模式",
      "target": "working-tree",
      "expected_behavior": "现有接口不能枚举原生特性列表，所需事实仍不可用，返回Unknown/AN3001；不把占位空当无记录",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S14",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapMethod / ReturnAttributes / attr(ret ...)",
      "conditions": "ReadonlyReturnAttributeIdentityTests：真实标记来自SelectedMarker，多个依赖声明同名类型；读取者提供/缺失实际标记依赖",
      "target": "working-tree",
      "expected_behavior": "按真实构造器作用域选类型；实际依赖缺失时ReturnAttributes不可用，需该事实的查询Unknown/AN3001，RefReadOnly及普通签名独立事实保留",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S15",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "Mapper / HasDirectAttribute",
      "conditions": "独立 Fody 测试程序集中的 CecilGenericParameterAttributeTests：真实类型/方法泛型形参声明，有标记/已知空，所属类型及方法均有标记，特性构造器抛异常",
      "target": "working-tree",
      "expected_behavior": "直接 API 查询形参自身记录，有标记 Matched、已知空 NotMatched，诊断为空且不实例化特性；不借用所属声明的标记",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-001",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S16",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper / HasDirectAttribute / attr(type ...)",
      "conditions": "GenericParameterAttributeTests：同一源码发射 PE 后导入 Roslyn，类型/方法泛型形参有、无标记；仅形参带标记的声明类型 DSL 对照",
      "target": "working-tree",
      "expected_behavior": "直接 API 查询形参自身记录，有标记 Matched、已知空 NotMatched，诊断为空且不实例化特性；不借用所属声明的标记；DSL type 仍指成员声明类型",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-001",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S17",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "HasDirectAttribute / AreEqual / attr(call ...)",
      "conditions": "QueryTargetContractTests 的同源真实程序集：非限定字段、指定完整程序集及错误版本/命名空间/种类/名称、泛型元数/实参、数组实参rank/shape与严格相等控制；精确/通配特性模式对照",
      "target": "working-tree",
      "expected_behavior": "查询侧空程序集名/空命名空间/Unknown kind分别不限；指定字段及递归结构保留约束；不符合条件NotMatched，正例Matched，AreEqual保持对称严格，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S18",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "HasDirectAttribute / AreEqual / attr(call ...)",
      "conditions": "QueryTargetContractTests 的同源真实程序集：非限定字段、指定完整程序集及错误版本/命名空间/种类/名称、泛型元数/实参、数组实参rank/shape与严格相等控制；精确/通配特性模式对照",
      "target": "working-tree",
      "expected_behavior": "查询侧空程序集名/空命名空间/Unknown kind分别不限；指定字段及递归结构保留约束；不符合条件NotMatched，正例Matched，AreEqual保持对称严格，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S19",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "HasDirectAttribute / AreEqual / attr(call ...)",
      "conditions": "QueryTargetContractTests 的同源真实程序集：非限定字段、指定完整程序集及错误版本/命名空间/种类/名称、泛型元数/实参、数组实参rank/shape与严格相等控制；精确/通配特性模式对照",
      "target": "working-tree",
      "expected_behavior": "查询侧空程序集名/空命名空间/Unknown kind分别不限；指定字段及递归结构保留约束；不符合条件NotMatched，正例Matched，AreEqual保持对称严格，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S20",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "HasDirectAttribute / AreEqual / attr(call ...)",
      "conditions": "QueryTargetContractTests 的同源真实程序集：非限定字段、指定完整程序集及错误版本/命名空间/种类/名称、泛型元数/实参、数组实参rank/shape与严格相等控制；精确/通配特性模式对照",
      "target": "working-tree",
      "expected_behavior": "查询侧空程序集名/空命名空间/Unknown kind分别不限；指定字段及递归结构保留约束；不符合条件NotMatched，正例Matched，AreEqual保持对称严格，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S21",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "HasDirectAttribute / explicit Mapper",
      "conditions": "DirectAttributeLimitTests 同源真实C#发射PE；CecilDirectAttributeLimitTests 公共Cecil对象及类型/方法泛型形参声明；不包含Fody；特性类型深度64/65/67、元数64/65、已知空列表及预先取消",
      "target": "working-tree",
      "expected_behavior": "查询内部原生特性类型超限返回Unknown和原AN3005/AN3006；上限内精确类型Matched、已知空NotMatched且无诊断；显式映射同一类型仍抛AspectNLimitException，预先取消仍传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S22",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "HasDirectAttribute / explicit Mapper",
      "conditions": "独立Fody测试程序集的CecilDirectAttributeLimitTests 公共Cecil对象及类型/方法泛型形参声明；不声称本轮Fody读取同源PE；特性类型深度64/65/67、元数64/65、已知空列表及预先取消",
      "target": "working-tree",
      "expected_behavior": "查询内部原生特性类型超限返回Unknown和原AN3005/AN3006；上限内精确类型Matched、已知空NotMatched且无诊断；显式映射同一类型仍抛AspectNLimitException，预先取消仍传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S23",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "HasDirectAttribute / explicit Mapper",
      "conditions": "DirectAttributeLimitTests 同源真实C#发射并由CLR加载的PE；特性类型深度64/65/67、元数64/65、已知空列表及预先取消",
      "target": "working-tree",
      "expected_behavior": "查询内部原生特性类型超限返回Unknown和原AN3005/AN3006；上限内精确类型Matched、已知空NotMatched且无诊断；显式映射同一类型仍抛AspectNLimitException，预先取消仍传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S24",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "HasDirectAttribute / explicit Mapper",
      "conditions": "DirectAttributeLimitTests 同源真实C#源码符号，发射成功作为合法输入前提；特性类型深度64/65/67、元数64/65、已知空列表及预先取消",
      "target": "working-tree",
      "expected_behavior": "查询内部原生特性类型超限返回Unknown和原AN3005/AN3006；上限内精确类型Matched、已知空NotMatched且无诊断；显式映射同一类型仍抛AspectNLimitException，预先取消仍传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S25",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "HasDirectAttribute / explicit Mapper",
      "conditions": "DirectAttributeLimitTests 同源真实C#发射PE后重新导入符号；特性类型深度64/65/67、元数64/65、已知空列表及预先取消",
      "target": "working-tree",
      "expected_behavior": "查询内部原生特性类型超限返回Unknown和原AN3005/AN3006；上限内精确类型Matched、已知空NotMatched且无诊断；显式映射同一类型仍抛AspectNLimitException，预先取消仍传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S26",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "Compile* / CompiledPattern.Match",
      "conditions": "GenericVariableModeTests、BindingBudgetTests：特性中首次声明默认AN2006；方法槽位先声明后的特性引用；Capture多记录/多目标历史回归及唯一数量限额",
      "target": "working-tree",
      "expected_behavior": "Declaration只能引用原候选已有槽位，不从记录重绑；Capture保留可满足分支，超限整次Unknown/AN3007",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-007",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    }
  ],
  "invariant_checks": [
    {
      "invariant_id": "INVLOGIC",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVLOGIC"
      },
      "reason": "条目包含匹配或关系结果，遵循三态组合；具体覆盖按场景",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    },
    {
      "invariant_id": "INVPARITY",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVPARITY"
      },
      "reason": "涉及适配器映射或关系时只比较同等输入和所需可用事实，不能外推缺失后端行",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    },
    {
      "invariant_id": "INVMISSING",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVMISSING"
      },
      "reason": "候选所需事实或原生关联可能不可用，不能把占位值当作确定事实",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    },
    {
      "invariant_id": "INVCANCEL",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVCANCEL"
      },
      "reason": "条目纳入完整匹配或公开关系入口，其 CancellationToken 必须按横向契约传播",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    }
  ],
  "relations": [
    {
      "type": "depends_on",
      "capability_id": "CROSS-AVAILABILITY-001"
    },
    {
      "type": "depends_on",
      "capability_id": "GEN-BINDING-001"
    },
    {
      "type": "depends_on",
      "capability_id": "MEM-SELECTORS-001"
    }
  ],
  "open_findings": [],
  "related_findings": [
    "FND-20261013-004",
    "FND-20261013-005",
    "FND-20261021-001",
    "FND-20261021-003",
    "FND-20261021-005"
  ]
}
-->

## 摘要

attr 筛选直接 CustomAttribute 元数据记录，特性写法、元数据标志、运行时伪特性和实例化对象不能混为一谈。

## 契约

type 为声明类型；call 为当前方法；prop 为关联属性；para[n]/para n 为指定零起参数；para[*]/para * 为任意参数；ret 为返回槽；exec 对访问器额外包含属性；any/* 为支持的直接位置。不继承特性，不实例化特性。

## 输入条件

空但已知的列表证明没有记录；不可用空列表不能证明没有记录。泛型特性按类型实参/模式变量匹配。参数下标与声明顺序一致，索引约束不自动重排。

## 分层能力

适配器过滤 flags/table 编码的伪特性，保留真实 PE CustomAttribute，包括与伪特性同类型的真实记录。Readonly 标记可用于分类并同时保留实际记录。数组声明类型不继承元素类型的特性。

## 阶段失败

未知属性关联或目标记录查询在需要时 Unknown；普通方法没有 prop 位置则 NotMatched。非法参数索引和旧长位置名是编译问题；不将错误和未知统一为 false。

直接查询内部读取、映射原生特性类型超过深度/元数上限时返回 Unknown，保留原 AN3005/AN3006；显式 Mapper 仍抛 `AspectNLimitException`。S21–S25 覆盖此入口区分及上限内、已知空和取消控制；Cecil/Fody 还核验泛型形参声明自身记录。具体裁决和输入范围见 [D01 修复记录](../../../reviews/2026-10-21-d01-direct-attribute-limits.md)。

## 例子与边界

```text
attr(call MarkerAttribute)
attr(para[0] MarkerAttribute)
attr(exec MarkerAttribute)
getter(string Name) && attr(call MarkerAttribute)
```

`[Serializable]`、`[DllImport]`、`[In]` 等写法可能被编码为 flags/其他表，不是独立真实 CA 记录；不能只凭源码有方括号期待 attr 命中。但 PE 真正写入的同类型 CA 仍可匹配。特性构造器抛异常不应影响纯元数据查询。

## 规范依据

语言第 14 节及可用性专题。排除理由不是“某次反射没读到属性”，而是正式直接记录契约。

## 实现与证据

[核心特性位置](../../../../tests/AspectN.Tests/Evaluation/AttributePatternTests.cs)、[真实记录一致性](../../../../tests/AspectN.Conformance.Tests/CustomAttributeRecordTests.cs)、[不实例化查询](../../../../tests/AspectN.Reflection.Tests/ReflectionMappingTests.cs)。S5的核心语法排除及S6–S9的特性继承排除已分别追加独立执行证据。已修复的PE记录身份问题和已裁决的detached精确查询差异见S10/S11及关联Finding。

## 关联与缺口

精确特性的 `HasDirectAttribute.attributeType` 是有方向的查询目标，字段不限及递归结构规则见公开 XML 和[后端实现指南](../../../usage/backend-implementation.md)。S17–S20 将四视图的直接查询、明确限定字段和严格相等控制单独登记；S1 的记录型后端只核对核心传参，不替代第三方匹配实现验证。

CROSS-AVAILABILITY-001 治理缺失事实，GEN-BINDING-001 治理泛型特性的绑定备选，MEM-SELECTORS-001 治理访问器域。Fody 的具体真实记录一致性未由三后端测试自动推出。

类型和方法的泛型形参声明可作为 `HasDirectAttribute(AttributeTarget.ForType(...), ...)` 的直接目标；有标记 Matched、已知空 NotMatched。Cecil 直接读取形参声明的记录，所属类型/方法上的标记不能替代形参记录；引用与声明分别保留原生关联，先后映射顺序不改变各自事实。S2–S4 与 S16 用同一编译输入覆盖 Cecil、Reflection、Roslyn 源码/PE，S15 单独运行 Fody 身份下的真实声明；这些证据不外推 Fody 的其他特性组合。`attr(type ...)` 仍选择成员的声明类型，未新增泛型形参 DSL 位置。具体回归见 [GenericParameterAttributeTests](../../../../tests/AspectN.Conformance.Tests/GenericParameterAttributeTests.cs) 和 [Cecil/Fody 回归](../../../../tests/AspectN.Cecil.Tests/CecilGenericParameterAttributeTests.cs)。

## 已知事实、原生回退与 PE 记录身份

已知完整 TypeAttributes 优先级来自维护者2026-10-14裁决；已知空也是事实，不能无条件转到原生快速路径。S11覆盖Reflection原/新上下文，不扩张直接后端 API 的脱离关联保证。

所需列表不可用时，S12的具体精确模式可由 HasDirectAttribute 恢复一个存在性事实；S13的通配、变量、复合模式仍需列表，接口没有枚举入口，也不能任意拆分同记录组合条件。不可用空是占位值。

Roslyn PE 隐藏 readonly 返回标记必须从原始 MethodDef、返回 Param 和 CA 构造器作用域恢复，不从读取 Compilation 任取同名类型。S10控制普通/嵌入标记及同名依赖；S14控制实际依赖缺失时 ReturnAttributes 不可用、RefReadOnly 等独立事实仍有效。具体[回归入口](../../../../tests/AspectN.Conformance.Tests/ReadonlyReturnAttributeIdentityTests.cs)与[类型特性上下文](../../../../tests/AspectN.Conformance.Tests/TypeAttributeContextTests.cs)按场景保存证据。

变量配置由 GEN-BINDING-001 管理。特性中的首次类型捕获需要 Capture；默认 Declaration 可引用已声明槽位。S1 动态捕获用例已明确迁移，S26 补默认诊断、固定引用及共享预算；真实记录映射、直接查询和 D01 通道不变。

2026-10-22资源配置变更按[实施审查](../../../reviews/2026-10-22-configurable-resource-limits.md)重评当前证据。历史报告不改写；新增配置的范围由CROSS-RESOURCES-001/S9—S15区分。未新增或改动N/A记录；没有从缺少某后端场景推断排除。
