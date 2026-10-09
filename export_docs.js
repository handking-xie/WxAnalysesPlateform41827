const fs = require('fs');
const path = require('path');

// 1. 读取 Apifox 文档
const apifoxPath = path.join(__dirname, '新版4.x_hook文档.apifox.json');
const doc = JSON.parse(fs.readFileSync(apifoxPath, 'utf8'));

function findApis(obj) {
  let res = [];
  if (obj && typeof obj === 'object') {
    if (obj.api && obj.api.path) res.push(obj);
    for (let k of Object.keys(obj)) res = res.concat(findApis(obj[k]));
  }
  return res;
}

const apifoxList = findApis(doc);
const apifoxMap = new Map();
for (const item of apifoxList) {
  const m = item.api.path.match(/\/api\/[a-zA-Z0-9_-]+/);
  if (m && !apifoxMap.has(m[0])) {
    apifoxMap.set(m[0], item);
  }
}

// 补充 Apifox 中路径拼写有误或名称别名映射
const extraApifox = {
  '/api/set_room_admin': apifoxList.find(a => (a.name || '').includes('添加群管理')),
  '/api/del_room_admin': apifoxList.find(a => (a.name || '').includes('删除群管理')),
  '/api/get_fav_list': apifoxList.find(a => (a.name || '').includes('获取收藏列表')),
  '/api/revoke_msg': apifoxList.find(a => (a.name || '').includes('撤回任何消息')),
  '/api/get_chatroom_detail': apifoxList.find(a => (a.name || '').includes('获取群详情缓存') || (a.name || '').includes('获取群详情')),
  '/api/bm_get_room_members': apifoxList.find(a => (a.name || '').includes('获取群成员列表')),
  '/api/bm_member_nick': apifoxList.find(a => (a.name || '').includes('获取群成员简要信息')),
  '/api/show_member_nick': apifoxList.find(a => (a.name || '').includes('获取群成员简要信息')),
  '/api/mod_chat_room_name': apifoxList.find(a => (a.name || '').includes('修改群名称')),
  '/api/update_contact': apifoxList.find(a => (a.name || '').includes('更新单个用户资料')),
  '/api/get_contact_list': apifoxList.find(a => (a.name || '').includes('获取好友资料')),
  '/api/get_profile_new': apifoxList.find(a => (a.name || '').includes('获取个人资料缓存')),
  '/api/net_scene_get_member_from_chat_room': apifoxList.find(a => (a.name || '').includes('查询群成员信息')),
  '/api/get_realfriend_list': apifoxList.find(a => (a.name || '').includes('获取好友资料')),
  '/api/get_gh_list': apifoxList.find(a => (a.name || '').includes('获取好友资料')),
  '/api/get_friend_by_labelId': apifoxList.find(a => (a.name || '').includes('根据标签获取好友')),
  '/api/get_room_a8key': apifoxList.find(a => (a.name || '').includes('a8key') || (a.name || '').includes('A8Key')),
  '/api/checksmscanaddcard': apifoxList.find(a => (a.name || '').includes('二维码') || (a.name || '').includes('扫码')),
};

// 手工逆向提取的隐藏/缺失接口定义
const manualDefs = {
  '/api/test': {
    name: '接口连通性测试',
    desc: '测试微信 Hook HTTP 服务是否正常运行',
    reqJson: {},
    params: [],
    resText: 'HTTP 接口响应：\n立即返回成功状态码，例如 `{"errCode": 1, "errMsg": "ok"}`。'
  },
  '/api/update_group_member_contact': {
    name: '更新群成员联系人信息',
    desc: '刷新指定群内某群成员的个人资料与联系人信息',
    reqJson: { roomId: '123456789@chatroom', wxid: 'wxid_xxxxxxxx' },
    params: [
      { name: 'roomId', type: 'string', desc: '群聊 ID（以 @chatroom 结尾）' },
      { name: 'wxid', type: 'string', desc: '群成员的微信 ID' }
    ],
    resText: 'HTTP 接口响应：\n立即返回 `{"errCode": 1}`。微信在后台向服务器请求更新联系人信息，更新完成后通过联系人更新事件推送。'
  },
  '/api/get_room_a8key': {
    name: '获取群聊链接 A8Key',
    desc: '请求群聊链接或群邀请的 A8Key 解析',
    reqJson: { roomId: '123456789@chatroom', url: 'https://weixin.qq.com/...' },
    params: [
      { name: 'roomId', type: 'string', desc: '群聊 ID' },
      { name: 'url', type: 'string', desc: '需要解析的链接或邀请链接' }
    ],
    resText: 'HTTP 接口响应：\n返回包含解析后 fullUrl / title / content 等相关信息的 JSON 数据。'
  },
  '/api/add_friend': {
    name: '主动添加好友',
    desc: '向目标微信用户发送好友添加申请',
    reqJson: { wxid: 'wxid_xxxxxxxx', content: '您好，我是...', scene: 15 },
    params: [
      { name: 'wxid', type: 'string', desc: '目标用户的微信号或原始 wxid' },
      { name: 'content', type: 'string', desc: '验证消息（申请添加附言）' },
      { name: 'scene', type: 'number', desc: '添加来源场景（如 15:手机号搜索，30:扫一扫二维码，14:群聊等）' }
    ],
    resText: 'HTTP 接口响应：\n返回 `{"errCode": 1}`，申请发送成功。'
  },
  '/api/verify_user': {
    name: '通过好友验证申请',
    desc: '同意并通过收到的好友添加请求',
    reqJson: { encryptUserName: 'v1_xxxxxxxx@stranger', ticket: 'v2_xxxxxxxx', scene: 6 },
    params: [
      { name: 'encryptUserName', type: 'string', desc: '好友请求推送中的加密用户名 (v1_...)' },
      { name: 'ticket', type: 'string', desc: '好友请求推送中的验证 ticket (v2_...)' },
      { name: 'scene', type: 'number', desc: '添加来源场景值' }
    ],
    resText: 'HTTP 接口响应：\n返回 `{"errCode": 1}`。通过后会触发好友关系变更推送与新好友首条欢迎消息。'
  },
  '/api/receivewxhb': {
    name: '接收/查看微信红包',
    desc: '查看微信红包详情，获取红包是否已被领取等状态',
    reqJson: { channelId: '1', msgType: '49', nativeUrl: 'wxpay://...', sendId: '1000039201...' },
    params: [
      { name: 'channelId', type: 'string', desc: '红包渠道 ID' },
      { name: 'msgType', type: 'string', desc: '红包消息类型数值（通常为 49）' },
      { name: 'nativeUrl', type: 'string', desc: '红包 XML 中解析出的 nativeUrl 链接' },
      { name: 'sendId', type: 'string', desc: '红包发送方 ID / 业务编号' }
    ],
    resText: 'HTTP 接口响应：\n返回红包基本信息及领取状态。'
  },
  '/api/openwxhb': {
    name: '打开微信红包 (拆红包)',
    desc: '执行拆红包操作，领取红包金额',
    reqJson: { channelId: '1', msgType: '49', nativeUrl: 'wxpay://...', sendId: '1000039201...', from_wxid: 'wxid_sender' },
    params: [
      { name: 'channelId', type: 'string', desc: '红包渠道 ID' },
      { name: 'msgType', type: 'string', desc: '消息类型（通常为 49）' },
      { name: 'nativeUrl', type: 'string', desc: '红包 nativeUrl 链接' },
      { name: 'sendId', type: 'string', desc: '红包唯一标识 sendId' },
      { name: 'from_wxid', type: 'string', desc: '发红包者的微信 ID' }
    ],
    resText: 'HTTP 接口响应：\n返回拆红包结果（金额、抢到红包用户列表等信息）。'
  },
  '/api/openwxhb_vip': {
    name: '打开定制红包 (VIP通道)',
    desc: '特定活动/企业定制红包拆取通道',
    reqJson: { channelId: '1', msgType: '49', nativeUrl: 'wxpay://...', sendId: '1000039201...' },
    params: [
      { name: 'channelId', type: 'string', desc: '红包渠道 ID' },
      { name: 'msgType', type: 'string', desc: '消息类型数值' },
      { name: 'nativeUrl', type: 'string', desc: '红包 nativeUrl 链接' },
      { name: 'sendId', type: 'string', desc: '红包 sendId' }
    ],
    resText: 'HTTP 接口响应：\n返回拆红包结果。'
  },
  '/api/generate_payqrcode': {
    name: '生成个人收款/支付二维码',
    desc: '生成指定金额或自定义格式的微信支付/收款二维码',
    reqJson: { url: 'weixin://wxpay/bizpayurl?...', urlType: 1 },
    params: [
      { name: 'url', type: 'string', desc: '支付或收款协议 URL' },
      { name: 'urlType', type: 'number', desc: '二维码类型' }
    ],
    resText: 'HTTP 接口响应：\n返回生成的二维码 base64 数据或解析后的二维码图片链接。'
  },
  '/api/sns_send_xml': {
    name: '发送 XML 自定义朋友圈',
    desc: '通过原始 XML 格式直接构造并发布朋友圈内容（支持图文、视频、音乐等富媒体卡片）',
    reqJson: { xml: '<TimelineObject><id>0</id><contentDesc>测试自定义朋友圈</contentDesc>...</TimelineObject>' },
    params: [
      { name: 'xml', type: 'string', desc: '标准完整的 TimelineObject 朋友圈 XML 字符串' }
    ],
    resText: 'HTTP 接口响应：\n立即返回 `{"errCode": 1}`。朋友圈发布成功后，服务器会推送该朋友圈的真实 SNS ID。'
  },
  '/api/sns_get_user_page2': {
    name: '获取指定好友朋友圈相册',
    desc: '获取指定好友的历史朋友圈时间线动态（支持分页）',
    reqJson: { to_wxid: 'wxid_xxxxxxxxxxxxxx', firstPageMd5: '', maxId: '0' },
    params: [
      { name: 'to_wxid', type: 'string', desc: '目标好友的微信 ID（如 wxid_xxx 或设置的微信号）' },
      { name: 'firstPageMd5', type: 'string', desc: '第一页 MD5 校验串，首次拉取或不校验可传空字符串 ""' },
      { name: 'maxId', type: 'string', desc: '翻页标识。第一页传入 "0"；获取下一页时，传入上一页最后一条朋友圈的 id' }
    ],
    resText: 'HTTP 接口响应：\n立即返回接口触发状态（`{"errCode": 1, ...}`）。\n\n朋友圈数据回调（主动推送）：\n微信服务器返回数据后，Hook 会通过配置的回调服务（HTTP 回调或 WebSocket）进行异步推送。\n推送数据格式：\n- `msgType`: `"朋友圈刷新"`\n- `username`: 对应好友的 wxid\n- `id`: 该条朋友圈的 ID（用于翻页 maxId）\n- `objectDesc.buffer`: Base64 编码的 `<TimelineObject>` XML（包含文字、时间戳及图片视频直链）'
  },
  '/api/get_user_nick': {
    name: '获取用户昵称与备注',
    desc: '获取指定联系人或群成员的昵称和备注',
    reqJson: { wxid: 'wxid_xxxxxxxx' },
    params: [
      { name: 'wxid', type: 'string', desc: '用户的微信 ID' }
    ],
    resText: 'HTTP 接口响应：\n返回用户昵称、备注、头像等详细对象。'
  },
  '/api/get_friend_by_labelId': {
    name: '根据标签获取好友列表',
    desc: '根据指定的标签 ID 查询该标签下的所有好友微信 ID 列表',
    reqJson: { label: '12345' },
    params: [
      { name: 'label', type: 'string', desc: '微信标签 ID（字符串或整型）' }
    ],
    resText: 'HTTP 接口响应：\n返回包含该标签下所有好友 wxid 列表的数组。'
  },
  '/api/sns_like': {
    name: '朋友圈点赞',
    desc: '对指定朋友圈动态执行点赞操作',
    reqJson: { sns_id: '14420282581074719279', to_wxid: 'wxid_owner' },
    params: [
      { name: 'sns_id', type: 'string', desc: '目标朋友圈的动态 ID' },
      { name: 'to_wxid', type: 'string', desc: '发布该朋友圈的作者微信 ID' }
    ],
    resText: 'HTTP 接口响应：\n返回 `{"errCode": 1}`，点赞操作成功。'
  },
  '/api/sns_unlike': {
    name: '取消朋友圈点赞',
    desc: '取消对某条朋友圈的赞',
    reqJson: { sns_id: '14420282581074719279', to_wxid: 'wxid_owner' },
    params: [
      { name: 'sns_id', type: 'string', desc: '目标朋友圈的动态 ID' },
      { name: 'to_wxid', type: 'string', desc: '发布该朋友圈的作者微信 ID' }
    ],
    resText: 'HTTP 接口响应：\n返回 `{"errCode": 1}`，取消点赞成功。'
  },
  '/api/sns_do_comment': {
    name: '发表朋友圈评论',
    desc: '在指定朋友圈动态下直接发表一级评论',
    reqJson: { sns_id: '14420282581074719279', comment: '评论内容', to_wxid: 'wxid_owner' },
    params: [
      { name: 'sns_id', type: 'string', desc: '目标朋友圈的动态 ID' },
      { name: 'comment', type: 'string', desc: '评论文本内容' },
      { name: 'to_wxid', type: 'string', desc: '发布该朋友圈的作者微信 ID' }
    ],
    resText: 'HTTP 接口响应：\n返回 `{"errCode": 1}` 及新生成的评论 ID。'
  },
  '/api/get_voice_trans': {
    name: '语音消息转文字',
    desc: '对收到的语音消息触发服务器语音识别转换为文字',
    reqJson: { clientMsgId: '1000293120', length: 1024 },
    params: [
      { name: 'clientMsgId', type: 'string', desc: '语音消息的客户端唯一消息 ID' },
      { name: 'length', type: 'number', desc: '语音数据长度' }
    ],
    resText: 'HTTP 接口响应：\n返回语音识别后的文本结果字符串。'
  },
  '/api/send_app_xml': {
    name: '发送原始 App 消息 XML',
    desc: '向好友或群聊发送原始构造的 App 消息 (如合并聊天记录、文件卡片、图文分享等)',
    reqJson: { roomId: 'wxid_target', xml: '<msg><appmsg appid="">...</appmsg></msg>' },
    params: [
      { name: 'roomId', type: 'string', desc: '接收者微信 ID 或群聊 ID' },
      { name: 'xml', type: 'string', desc: 'App 消息的原始 XML 数据' }
    ],
    resText: 'HTTP 接口响应：\n返回 `{"errCode": 1}`，发送成功。'
  },
  '/api/auto_login': {
    name: '自动登录微信',
    desc: '唤起并尝试使用本地已保存的 Session/凭据自动登录微信',
    reqJson: {},
    params: [],
    resText: 'HTTP 接口响应：\n返回自动登录状态结果。'
  }
};

// 2. 读取 unique_routes.json
const uniqueRoutes = JSON.parse(fs.readFileSync(path.join(__dirname, 'unique_routes.json'), 'utf8'));

// 3. 模块分类映射
const modules = [
  {
    name: '一、消息发送与交互模块 (Messages)',
    desc: '包含向好友或群聊发送文本、@群成员、图片、语音、MP3音频、文件、卡片、表情、XML 以及消息撤回等接口。',
    paths: [
      '/api/send_text_msg',
      '/api/send_at_text',
      '/api/send_image_msg',
      '/api/send_voice',
      '/api/send_mp3_voice',
      '/api/send_file_msg',
      '/api/send_card_msg',
      '/api/send_emotion_msg',
      '/api/send_xml',
      '/api/send_app_xml',
      '/api/send_app_msg',
      '/api/revoke_msg'
    ]
  },
  {
    name: '二、朋友圈模块 (Moments / SNS)',
    desc: '包含发布朋友圈、朋友圈首页/下一页时间线抓取、好友个人朋友圈获取、朋友圈详情、点赞、评论与删除等全功能接口。',
    paths: [
      '/api/sns_get_user_page2',
      '/api/sns_get_first_page',
      '/api/sns_get_next_page',
      '/api/sns_get_detail',
      '/api/sns_post',
      '/api/sns_send_img',
      '/api/sns_send_xml',
      '/api/sns_like',
      '/api/sns_unlike',
      '/api/sns_do_comment',
      '/api/sns_comment_reply',
      '/api/sns_del_comment',
      '/api/sns_del',
      '/api/sns_upload'
    ]
  },
  {
    name: '三、联系人与好友管理模块 (Contacts & Friends)',
    desc: '包含好友信息查询、好友添加、验证申请、备注修改、删除好友、修改个人信息、公众号列表等接口。',
    paths: [
      '/api/get_contact_list',
      '/api/get_contact_fast',
      '/api/update_contact',
      '/api/update_all_friend',
      '/api/get_realfriend_list',
      '/api/get_frien_lists',
      '/api/net_scene_search_contact',
      '/api/get_profile_new',
      '/api/get_user_nick',
      '/api/add_friend',
      '/api/verify_friend',
      '/api/verify_user',
      '/api/remark_contact',
      '/api/del_contact',
      '/api/mod_self_nick_name',
      '/api/mod_self_nick_signature',
      '/api/upload_head_img',
      '/api/get_my_qrocde',
      '/api/get_lbs_friend',
      '/api/get_gh_list'
    ]
  },
  {
    name: '四、群聊管理模块 (Chatrooms)',
    desc: '包含创建群聊、邀请成员、踢出成员、退出群聊、设置管理员、修改群名、修改群内昵称、群公告等接口。',
    paths: [
      '/api/creat_chat_room',
      '/api/add_member_to_chat_room',
      '/api/invite_member_to_chat_room',
      '/api/del_member_from_chat_room',
      '/api/quit_and_del_chat_room',
      '/api/get_chatroom_list',
      '/api/get_chatroom_detail',
      '/api/bm_get_room_members',
      '/api/get_group_memeber_info',
      '/api/update_group_member_contact',
      '/api/set_room_admin',
      '/api/del_room_admin',
      '/api/mod_chat_room_name',
      '/api/mod_chat_room_self_nick_name',
      '/api/bm_member_nick',
      '/api/show_member_nick',
      '/api/set_room_announcement_pb',
      '/api/transferchatroomowner',
      '/api/enter_room',
      '/api/save_chatroom_to_contact',
      '/api/remov_chatroom_to_contact',
      '/api/get_room_a8key',
      '/api/net_scene_get_member_from_chat_room'
    ]
  },
  {
    name: '五、标签管理模块 (Labels)',
    desc: '包含微信标签列表查询、创建标签、删除标签、为联系人打标签以及按标签查好友等接口。',
    paths: [
      '/api/get_label_lists',
      '/api/add_label',
      '/api/del_label',
      '/api/modify_contact_label',
      '/api/get_friend_by_labelId'
    ]
  },
  {
    name: '六、多媒体与资源下载模块 (Media & Downloads)',
    desc: '包含下载高清图片、下载视频文件、下载通用文件、下载语音文件、语音转文字等接口。',
    paths: [
      '/api/download_img',
      '/api/download_video',
      '/api/download_file',
      '/api/download_voice',
      '/api/get_voice_trans',
      '/api/get_config_path'
    ]
  },
  {
    name: '七、微信支付与红包模块 (Pay & RedPackets)',
    desc: '包含生成支付二维码、转账自动确认/退还、接收红包、拆红包以及企业定制红包操作等接口。',
    paths: [
      '/api/generate_payqrcode',
      '/api/ten_pay_trans_fer_confirm',
      '/api/un_ten_pay_trans_fer_confirm',
      '/api/receivewxhb',
      '/api/openwxhb',
      '/api/openwxhb_vip'
    ]
  },
  {
    name: '八、系统与底层数据库模块 (System & Database)',
    desc: '包含微信初始化、接口健康检查、消息防撤回控制、获取A8Key、网页登录凭证、SQLite数据库句柄及执行、收藏列表等高级接口。',
    paths: [
      '/api/wechat_init',
      '/api/test',
      '/api/anti_revoke',
      '/api/get_a8key',
      '/api/js_login',
      '/api/auto_login',
      '/api/get_db_handle',
      '/api/sqlite3_exec',
      '/api/get_fav_list'
    ]
  }
];

// 组装每个 API 的格式化内容
function generateApiSection(routePath, index) {
  let apifoxItem = apifoxMap.get(routePath) || extraApifox[routePath];
  let manual = manualDefs[routePath];

  let name = '';
  let desc = '';
  let method = 'POST';
  let reqJson = {};
  let paramList = [];
  let resText = '';

  if (manual) {
    name = manual.name;
    desc = manual.desc;
    reqJson = manual.reqJson;
    paramList = manual.params;
    resText = manual.resText;
  } else if (apifoxItem) {
    name = apifoxItem.name || apifoxItem.api.name || '';
    desc = apifoxItem.api.description || '';
    method = (apifoxItem.api.method || 'POST').toUpperCase();

    // 提取请求参数
    if (apifoxItem.api.requestBody) {
      const rb = apifoxItem.api.requestBody;
      if (rb.data && typeof rb.data === 'string') {
        try {
          reqJson = JSON.parse(rb.data);
        } catch (e) {
          // ignore
        }
      } else if (rb.examples && rb.examples[0] && rb.examples[0].value) {
        try {
          reqJson = JSON.parse(rb.examples[0].value);
        } catch (e) {
          // ignore
        }
      }

      if (rb.jsonSchema && rb.jsonSchema.properties) {
        const props = rb.jsonSchema.properties;
        const required = rb.jsonSchema.required || [];
        for (let k of Object.keys(props)) {
          if (Object.keys(reqJson).length === 0 || !(k in reqJson)) {
            reqJson[k] = props[k].type === 'number' || props[k].type === 'integer' ? 0 : '';
          }
          paramList.push({
            name: k,
            type: props[k].type || 'string',
            desc: (props[k].description || '') + (required.includes(k) ? ' (必填)' : ' (可选)')
          });
        }
      }
    }

    // 响应说明
    let sampleRes = '';
    if (apifoxItem.api.responses && apifoxItem.api.responses[0] && apifoxItem.api.responses[0].responseExamples && apifoxItem.api.responses[0].responseExamples[0]) {
      sampleRes = apifoxItem.api.responses[0].responseExamples[0].data || '';
    }
    resText = 'HTTP 接口响应：\n立即返回调用状态或数据结果。';
    if (sampleRes && sampleRes !== '{}') {
      resText += '\n```json\n' + sampleRes.trim() + '\n```';
    } else {
      resText += '\n```json\n{\n  "errCode": 1,\n  "errMsg": "成功"\n}\n```';
    }
  } else {
    name = routePath.replace('/api/', '');
    desc = '底层已实现的 Hook API 接口';
    resText = 'HTTP 接口响应：\n立即返回 `{"errCode": 1}`。';
  }

  // 格式化参数表格
  let tableMd = '';
  if (paramList.length > 0) {
    tableMd = '\n| 参数名 | 类型 | 说明 |\n| :--- | :--- | :--- |\n';
    for (let p of paramList) {
      tableMd += `| \`${p.name}\` | \`${p.type}\` | ${p.desc || '-'} |\n`;
    }
  } else {
    tableMd = '\n*无需请求体参数或参数为空对象。*\n';
  }

  const jsonStr = JSON.stringify(reqJson, null, 2);

  return `### ${index}. ${name} (\`${routePath}\`)

${desc ? `> **功能说明**：${desc}\n` : ''}
#### 1. 接口地址与方法
- **URL**: \`http://127.0.0.1:19088${routePath}\`
- **Method**: \`${method}\`
- **Content-Type**: \`application/json\`

#### 2. 请求参数 (JSON)
\`\`\`json
${jsonStr}
\`\`\`
${tableMd}
#### 3. 数据接收与解析机制
${resText}

---
`;
}

// 4. 生成完整 Markdown
let md = `# libGLESv1.dll (WeChat 4.1.8.27 Hook) 接口完整导出文档

> **文档说明**：
> 本文档基于逆向分析提取自核心注入动态库 [libGLESv1.dll](file:///d:/WxHookSource41827/libGLESv1.dll)，并交叉校验了 [新版4.x_hook文档.apifox.json](file:///d:/WxHookSource41827/%E6%96%B0%E7%89%884.x_hook%E6%96%87%E6%A1%A3.apifox.json)。
> 覆盖了底层注册并完整实现的全部 **95 个 API 接口**（包含原 Apifox 文档中遗漏或拼写错误的 35+ 个隐藏接口，如好友个人朋友圈、朋友圈点赞/评论、红包拆领、好友添加/验证等）。
> 
> - **默认服务端口**：\`19088\`
> - **默认主机地址**：\`http://127.0.0.1:19088\`
> - **数据通信方式**：HTTP 请求触发 + Webhook / WebSocket 异步事件回调

---

## 目录索引 (Index)

`;

// 生成目录
let apiCounter = 1;
const apiMap = new Map();
uniqueRoutes.forEach(r => apiMap.set(r.path, r));

for (const mod of modules) {
  md += `### ${mod.name}\n`;
  for (const p of mod.paths) {
    if (apiMap.has(p)) {
      const item = apifoxMap.get(p) || extraApifox[p] || manualDefs[p];
      const title = item ? (item.name || item.api.name) : p;
      md += `- [${apiCounter}. ${title} (\`${p}\`)](#${apiCounter}-${encodeURIComponent(title.toLowerCase().replace(/[\s\(\)\/]/g, ''))})\n`;
      apiCounter++;
    }
  }
  md += '\n';
}

md += '---\n\n## 接口详细规范\n\n';

// 生成具体接口详情
apiCounter = 1;
for (const mod of modules) {
  md += `## ${mod.name}\n\n> ${mod.desc}\n\n`;
  for (const p of mod.paths) {
    if (apiMap.has(p)) {
      md += generateApiSection(p, apiCounter);
      apiCounter++;
      apiMap.delete(p);
    }
  }
}

// 检查是否有剩余未归类的接口
if (apiMap.size > 0) {
  md += '## 九、其他未分类接口\n\n';
  for (const [p, r] of apiMap.entries()) {
    md += generateApiSection(p, apiCounter);
    apiCounter++;
  }
}

// 写入目标文件
const outputPath = path.join(__dirname, 'libGLESv1_接口完整导出文档.md');
fs.writeFileSync(outputPath, md, 'utf8');
console.log('Successfully generated markdown to:', outputPath);
console.log('Total documented APIs:', apiCounter - 1);
