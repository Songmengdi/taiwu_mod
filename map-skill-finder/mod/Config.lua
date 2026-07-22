return {
	Title = "太吾寻访",
	Author = "SMD",
	Version = "1.0.2.0",
	FileId = 3767832260,
	GameVersion = "1.0.58.0",
	Description = "在任意未被侵袭的地域寻访功法书、技艺书、人物与商会。\n\n【功能一览】\n· 功法书：按地域、门派、功法分类选择目标功法与书页（方向、完整状态），查看持有人及其私人藏书/背包持有情况；自动给出最多三人的最少持有人拼书组合。\n· 技艺书：点击技艺分类即列出该分类全部技艺书与持有人，无需逐级筛选。\n· 人物：按姓名、性别、身份品级及最多三条资质/造诣条件（弹窗设置，不占界面）筛选人物；结果支持排序，与太吾同地域的人物可直接定位地格。\n· 商会：选择商会目标后自动查询商队位置，同地域商队可在表格中直接定位。\n· 查询地域可任意选择，也可一键使用太吾当前所在地域；选中目标后可标记地格，在地图上高亮提示。\n\n【使用方法】\n1. 订阅本 Mod 及前置 Mod「Taiwu UI Framework」（Steam 会自动一并订阅）。\n2. 进入存档后，点击界面下方「查找」按钮上方新增的寻访按钮，打开「寻访中心」。\n\n【兼容性】\n适配游戏 1.0.58.0。只读游戏数据，不修改存档。",
	Cover = "Cover.jpg",
	WorkshopCover = "Cover.jpg",
	Visibility = 0,
	Dependencies = {
		[1] = 3767831883,
	},
	BackendPlugins = {
		[1] = "MapSkillFinder.Backend.dll",
	},
	FrontendPlugins = {
		[1] = "MapSkillFinder.Frontend.dll",
	},
	Source = 0,
	HasArchive = false,
	NeedRestartWhenSettingChanged = false,
	ChangeConfig = false,
	DefaultSettings = { },
	TagList = {
		[1] = "Modifications",
		[2] = "Compatible Mods",
	},
	UpdateLogList = {
		[1] = {
			Timestamp = 1784474418,
		},
		[2] = {
			Timestamp = 1784564495,
		},
		[3] = {
			Timestamp = 1784649598,
		},
	},
}
