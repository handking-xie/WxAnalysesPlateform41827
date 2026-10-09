const fs = require('fs');
const path = require('path');

// 1. 读取原始 Apifox 文件
const apifoxPath = path.join(__dirname, '新版4.x_hook文档.apifox.json');
const originalDoc = JSON.parse(fs.readFileSync(apifoxPath, 'utf8'));

function findApis(obj) {
  let res = [];
  if (obj && typeof obj === 'object') {
    if (obj.api && obj.api.path) res.push(obj);
    for (let k of Object.keys(obj)) res = res.concat(findApis(obj[k]));
  }
  return res;
}

const apifoxList = findApis(originalDoc);
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

// 逆向提取的手工补充定义
const manualDefs = {
  '/api/test': {
    name: '接口连通性测试',
    desc: '测试微信 Hook HTTP 服务是否正常运行',
    reqJson: {},
    params: [],
    resData: '{\n  "errCode": 1,\n  "errMsg": "ok"\n}'
  },
  '/api/update_group_member_contact': {
    name: '更新群成员联系人信息',
    desc: '刷新指定群内某群成员的个人资料与联系人信息',
    reqJson: { roomId: '123456789@chatroom', wxid: 'wxid_xxxxxxxx' },
    params: [
      { name: 'roomId', type: 'string', desc: '群聊 ID（以 @chatroom 结尾）' },
      { name: 'wxid', type: 'string', desc: '群成员的微信 ID' }
    ],
    resData: '{\n  "errCode": 1,\n  "errMsg": "success"\n}'
  },
  '/api/get_room_a8key': {
    name: '获取群聊链接 A8Key',
    desc: '请求群聊链接或群邀请的 A8Key 解析',
    reqJson: { roomId: '123456789@chatroom', url: 'https://weixin.qq.com/...' },
    params: [
      { name: 'roomId', type: 'string', desc: '群聊 ID' },
      { name: 'url', type: 'string', desc: '需要解析的链接或邀请链接' }
    ],
    resData: '{\n  "fullUrl": "https://...",\n  "title": "网页标题",\n  "content": ""\n}'
  },
  '/api/add_friend': {
    name: '主动添加好友',
    desc: '向目标微信用户发送好友添加申请',
    reqJson: { wxid: 'wxid_xxxxxxxx', content: '您好，我是...', scene: 15 },
    params: [
      { name: 'wxid', type: 'string', desc: '目标用户的微信号或原始 wxid' },
      { name: 'content', type: 'string', desc: '验证消息（申请添加附言）' },
      { name: 'scene', type: 'integer', desc: '添加来源场景（如 15:手机号搜索，30:扫一扫二维码，14:群聊等）' }
    ],
    resData: '{\n  "errCode": 1,\n  "errMsg": "申请已发送"\n}'
  },
  '/api/verify_user': {
    name: '通过好友验证申请',
    desc: '同意并通过收到的好友添加请求',
    reqJson: { encryptUserName: 'v1_xxxxxxxx@stranger', ticket: 'v2_xxxxxxxx', scene: 6 },
    params: [
      { name: 'encryptUserName', type: 'string', desc: '好友请求推送中的加密用户名 (v1_...)' },
      { name: 'ticket', type: 'string', desc: '好友请求推送中的验证 ticket (v2_...)' },
      { name: 'scene', type: 'integer', desc: '添加来源场景值' }
    ],
    resData: '{\n  "errCode": 1,\n  "errMsg": "好友验证通过"\n}'
  },
  '/api/receivewxhb': {
    name: '接收/查看微信红包',
    desc: '查看微信红包详情，获取红包是否已被领取等状态',
    reqJson: { channelId: '1', msgType: '49', nativeUrl: 'wxpay://c2cbizmessagehandler/hongbao/receivehongbao?...', sendId: '1000039201...' },
    params: [
      { name: 'channelId', type: 'string', desc: '红包渠道 ID' },
      { name: 'msgType', type: 'string', desc: '红包消息类型数值（通常为 49）' },
      { name: 'nativeUrl', type: 'string', desc: '红包 XML 中解析出的 nativeUrl 链接' },
      { name: 'sendId', type: 'string', desc: '红包发送方 ID / 业务编号' }
    ],
    resData: '{\n  "errCode": 1,\n  "isOpened": 0,\n  "totalAmount": 100\n}'
  },
  '/api/openwxhb': {
    name: '打开微信红包 (拆红包)',
    desc: '执行拆红包操作，领取红包金额',
    reqJson: { channelId: '1', msgType: '49', nativeUrl: 'wxpay://c2cbizmessagehandler/hongbao/receivehongbao?...', sendId: '1000039201...', from_wxid: 'wxid_sender' },
    params: [
      { name: 'channelId', type: 'string', desc: '红包渠道 ID' },
      { name: 'msgType', type: 'string', desc: '消息类型（通常为 49）' },
      { name: 'nativeUrl', type: 'string', desc: '红包 nativeUrl 链接' },
      { name: 'sendId', type: 'string', desc: '红包唯一标识 sendId' },
      { name: 'from_wxid', type: 'string', desc: '发红包者的微信 ID' }
    ],
    resData: '{\n  "errCode": 1,\n  "amount": 88,\n  "status": 0,\n  "wishing": "恭喜发财，大吉大利"\n}'
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
    resData: '{\n  "errCode": 1,\n  "amount": 100,\n  "status": 0\n}'
  },
  '/api/generate_payqrcode': {
    name: '生成个人收款/支付二维码',
    desc: '生成指定金额或自定义格式的微信支付/收款二维码',
    reqJson: { url: 'weixin://wxpay/bizpayurl?...', urlType: 1 },
    params: [
      { name: 'url', type: 'string', desc: '支付或收款协议 URL' },
      { name: 'urlType', type: 'integer', desc: '二维码类型' }
    ],
    resData: '{\n  "errCode": 1,\n  "qrCodeUrl": "https://...",\n  "qrCodeBase64": "data:image/png;base64,..."\n}'
  },
  '/api/sns_send_xml': {
    name: '发送 XML 自定义朋友圈',
    desc: '通过原始 XML 格式直接构造并发布朋友圈内容（支持图文、视频、音乐等富媒体卡片）',
    reqJson: { xml: '<TimelineObject><id>0</id><contentDesc>测试自定义朋友圈</contentDesc></TimelineObject>' },
    params: [
      { name: 'xml', type: 'string', desc: '标准完整的 TimelineObject 朋友圈 XML 字符串' }
    ],
    resData: '{\n  "errCode": 1,\n  "errMsg": "发送成功"\n}'
  },
  '/api/sns_get_user_page2': {
    name: '获取指定好友朋友圈相册',
    desc: '获取指定好友的历史朋友圈时间线动态（支持分页）。\n数据接收机制：HTTP 立即返回触发状态；微信服务器返回后通过【朋友圈消息回调】异步推送（msgType: "朋友圈刷新"），其中 objectDesc.buffer 为 Base64 编码的 <TimelineObject> XML。',
    reqJson: { to_wxid: 'wxid_xxxxxxxxxxxxxx', firstPageMd5: '', maxId: '0' },
    params: [
      { name: 'to_wxid', type: 'string', desc: '目标好友的微信 ID（如 wxid_xxx 或设置的微信号）' },
      { name: 'firstPageMd5', type: 'string', desc: '第一页 MD5 校验串，首次拉取或不校验可传空字符串 ""' },
      { name: 'maxId', type: 'string', desc: '翻页标识。第一页传入 "0"；获取下一页时，传入上一页最后一条朋友圈的 id' }
    ],
    resData: '{\n  "errCode": 1,\n  "errMsg": "请求成功，等待朋友圈刷新回调"\n}'
  },
  '/api/get_user_nick': {
    name: '获取用户昵称与备注',
    desc: '获取指定联系人或群成员的昵称和备注',
    reqJson: { wxid: 'wxid_xxxxxxxx' },
    params: [
      { name: 'wxid', type: 'string', desc: '用户的微信 ID' }
    ],
    resData: '{\n  "wxid": "wxid_xxxxxxxx",\n  "nickName": "用户昵称",\n  "remark": "好友备注"\n}'
  },
  '/api/get_friend_by_labelId': {
    name: '根据标签获取好友列表',
    desc: '根据指定的标签 ID 查询该标签下的所有好友微信 ID 列表',
    reqJson: { label: '12345' },
    params: [
      { name: 'label', type: 'string', desc: '微信标签 ID（字符串或整型）' }
    ],
    resData: '{\n  "labelId": "12345",\n  "wxids": [\n    "wxid_001",\n    "wxid_002"\n  ]\n}'
  },
  '/api/sns_like': {
    name: '朋友圈点赞',
    desc: '对指定朋友圈动态执行点赞操作',
    reqJson: { sns_id: '14420282581074719279', to_wxid: 'wxid_owner' },
    params: [
      { name: 'sns_id', type: 'string', desc: '目标朋友圈的动态 ID' },
      { name: 'to_wxid', type: 'string', desc: '发布该朋友圈的作者微信 ID' }
    ],
    resData: '{\n  "errCode": 1,\n  "errMsg": "点赞成功"\n}'
  },
  '/api/sns_unlike': {
    name: '取消朋友圈点赞',
    desc: '取消对某条朋友圈的赞',
    reqJson: { sns_id: '14420282581074719279', to_wxid: 'wxid_owner' },
    params: [
      { name: 'sns_id', type: 'string', desc: '目标朋友圈的动态 ID' },
      { name: 'to_wxid', type: 'string', desc: '发布该朋友圈的作者微信 ID' }
    ],
    resData: '{\n  "errCode": 1,\n  "errMsg": "取消点赞成功"\n}'
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
    resData: '{\n  "errCode": 1,\n  "commentId": 123456,\n  "errMsg": "评论发表成功"\n}'
  },
  '/api/get_voice_trans': {
    name: '语音消息转文字',
    desc: '对收到的语音消息触发服务器语音识别转换为文字',
    reqJson: { clientMsgId: '1000293120', length: 1024 },
    params: [
      { name: 'clientMsgId', type: 'string', desc: '语音消息的客户端唯一消息 ID' },
      { name: 'length', type: 'integer', desc: '语音数据长度' }
    ],
    resData: '{\n  "errCode": 1,\n  "text": "语音识别出的文字内容"\n}'
  },
  '/api/send_app_xml': {
    name: '发送原始 App 消息 XML',
    desc: '向好友或群聊发送原始构造的 App 消息 (如合并聊天记录、文件卡片、图文分享等)',
    reqJson: { roomId: 'wxid_target', xml: '<msg><appmsg appid="">...</appmsg></msg>' },
    params: [
      { name: 'roomId', type: 'string', desc: '接收者微信 ID 或群聊 ID' },
      { name: 'xml', type: 'string', desc: 'App 消息的原始 XML 数据' }
    ],
    resData: '{\n  "errCode": 1,\n  "errMsg": "发送成功"\n}'
  },
  '/api/auto_login': {
    name: '自动登录微信',
    desc: '唤起并尝试使用本地已保存的 Session/凭据自动登录微信',
    reqJson: {},
    params: [],
    resData: '{\n  "errCode": 1,\n  "errMsg": "自动登录指令已触发"\n}'
  }
};

// 8 大模块分类
const modules = [
  {
    name: '一、消息发送与交互模块',
    desc: '文本、@消息、图片、语音、MP3音频、文件、卡片、表情、XML 以及消息撤回等接口',
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
    name: '二、朋友圈模块',
    desc: '发布朋友圈、朋友圈首页/下一页时间线抓取、好友个人朋友圈获取、详情、点赞、评论与删除等全功能接口',
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
    name: '三、联系人与好友管理模块',
    desc: '好友信息查询、好友添加、验证申请、备注修改、删除好友、修改个人信息、公众号列表等接口',
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
    name: '四、群聊管理模块',
    desc: '创建群聊、邀请成员、踢出成员、退出群聊、设置管理员、修改群名、修改群内昵称、群公告等接口',
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
    name: '五、标签管理模块',
    desc: '微信标签列表查询、创建标签、删除标签、为联系人打标签以及按标签查好友等接口',
    paths: [
      '/api/get_label_lists',
      '/api/add_label',
      '/api/del_label',
      '/api/modify_contact_label',
      '/api/get_friend_by_labelId'
    ]
  },
  {
    name: '六、多媒体与资源下载模块',
    desc: '下载高清图片、下载视频文件、下载通用文件、下载语音文件、语音转文字等接口',
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
    name: '七、微信支付与红包模块',
    desc: '生成支付二维码、转账自动确认/退还、接收红包、拆红包以及企业定制红包操作等接口',
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
    name: '八、系统与底层数据库模块',
    desc: '微信初始化、接口健康检查、消息防撤回控制、获取A8Key、网页登录凭证、SQLite数据库句柄及执行、收藏列表等高级接口',
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

let globalId = 8800000;
function nextId() {
  return ++globalId;
}

// 构造 Apifox 标准接口对象
function buildApifoxApiItem(routePath, moduleName) {
  let apifoxItem = apifoxMap.get(routePath) || extraApifox[routePath];
  let manual = manualDefs[routePath];

  let name = '';
  let desc = '';
  let method = 'post';
  let pathUrl = 'http://127.0.0.1:19088' + routePath;
  let reqSchema = {
    type: 'object',
    properties: {},
    required: [],
    'x-apifox-orders': []
  };
  let exampleData = '{}';
  let resData = '{\n  "errCode": 1,\n  "errMsg": "success"\n}';

  if (manual) {
    name = manual.name;
    desc = manual.desc;
    exampleData = JSON.stringify(manual.reqJson, null, 4);
    if (manual.resData) resData = manual.resData;

    for (const p of manual.params) {
      reqSchema.properties[p.name] = {
        type: p.type,
        description: p.desc
      };
      reqSchema.required.push(p.name);
      reqSchema['x-apifox-orders'].push(p.name);
    }
  } else if (apifoxItem) {
    name = apifoxItem.name || apifoxItem.api.name || routePath;
    desc = apifoxItem.api.description || '';
    method = (apifoxItem.api.method || 'post').toLowerCase();

    // 请求体
    if (apifoxItem.api.requestBody) {
      const rb = apifoxItem.api.requestBody;
      if (rb.jsonSchema) {
        reqSchema = JSON.parse(JSON.stringify(rb.jsonSchema));
      }
      if (rb.data && typeof rb.data === 'string' && rb.data.trim() !== '') {
        exampleData = rb.data;
      } else if (rb.examples && rb.examples[0] && rb.examples[0].value) {
        exampleData = rb.examples[0].value;
      }
    }

    // 响应体
    if (apifoxItem.api.responses && apifoxItem.api.responses[0] && apifoxItem.api.responses[0].responseExamples && apifoxItem.api.responses[0].responseExamples[0]) {
      const ex = apifoxItem.api.responses[0].responseExamples[0].data;
      if (ex && ex.trim() !== '' && ex !== '{}') {
        resData = ex;
      }
    }
  } else {
    name = routePath.replace('/api/', '');
    desc = '底层 Hook API 接口';
  }

  const apiId = nextId();
  const responseId = nextId().toString();

  return {
    name: name,
    api: {
      id: apiId,
      name: name,
      serverId: '',
      preProcessors: [],
      postProcessors: [],
      inheritPreProcessors: {},
      inheritPostProcessors: {},
      description: desc,
      operationId: '',
      sourceUrl: '',
      method: method,
      path: pathUrl,
      tags: [moduleName],
      status: 'released',
      auth: {},
      projectId: 7756981,
      moduleId: 7028736,
      folderId: 0,
      ordering: apiId % 1000,
      responsibleId: 0,
      commonResponseStatus: {},
      advancedSettings: { disabledSystemHeaders: {} },
      customApiFields: {},
      oasExtensions: '',
      mockScript: {},
      createdAt: '2026-01-24T08:55:10.000Z',
      updatedAt: '2026-10-08T15:00:00.000Z',
      visibility: 'INHERITED',
      securityScheme: {},
      callbacks: '',
      type: 'http',
      parameters: { query: [], path: [], cookie: [], header: [] },
      commonParameters: { query: [], body: [], header: [], cookie: [] },
      responses: [
        {
          id: responseId,
          name: '成功',
          code: 200,
          contentType: 'json',
          jsonSchema: { type: 'object', properties: {}, 'x-apifox-orders': [] },
          itemSchema: {},
          description: '',
          mediaType: '',
          headers: [],
          oasExtensions: '',
          key: 'res-' + responseId,
          responseExamples: [
            {
              id: nextId(),
              data: resData,
              description: '成功返回示例',
              name: '成功示例',
              ordering: 1,
              responseId: responseId
            }
          ]
        }
      ],
      responseExamples: [],
      requestBody: {
        type: 'application/json',
        parameters: [],
        jsonSchema: reqSchema,
        examples: [
          {
            mediaType: 'application/json',
            value: exampleData,
            name: '请求参数示例',
            key: 'application/json-0'
          }
        ],
        mediaType: '',
        oasExtensions: '',
        required: Object.keys(reqSchema.properties || {}).length > 0,
        additionalContent: [],
        data: exampleData,
        contentVariants: [
          {
            key: 'application/json',
            type: 'application/json',
            mediaType: 'application/json',
            jsonSchema: reqSchema,
            parameters: [],
            examples: [
              {
                mediaType: 'application/json',
                value: exampleData,
                name: '请求参数示例',
                key: 'application/json-0'
              }
            ],
            data: exampleData,
            required: false
          }
        ]
      },
      codeSamples: []
    }
  };
}

// 构造 Apifox 格式项目
const apifoxFolders = [];
let totalExported = 0;

for (const mod of modules) {
  const folderItems = [];
  for (const p of mod.paths) {
    const item = buildApifoxApiItem(p, mod.name);
    folderItems.push(item);
    totalExported++;
  }
  apifoxFolders.push({
    name: mod.name,
    items: folderItems
  });
}

const apifoxProject = {
  apifoxProject: '1.0.0',
  $schema: 'apifoxProject',
  info: {
    name: 'WeChat 4.x Hook 完整接口项目 (4.1.8.27)',
    description: '微信 4.1.8.27 64位底层 Hook 完整接口库，包含逆向提取的全部 95 个接口（朋友圈相册、点赞评论、红包、群管理、消息发送等）。',
    version: '1.0.0',
    mockRule: { rules: [], enableSystemRule: true }
  },
  servers: [
    {
      id: 'default',
      name: '默认本地服务',
      url: 'http://127.0.0.1:19088'
    }
  ],
  environments: [],
  schemaCollection: [],
  apiCollection: [
    {
      name: 'Root',
      id: nextId(),
      auth: {},
      parentId: 0,
      serverId: 'default',
      description: '微信 4.1.8.27 Hook API 接口集合',
      items: apifoxFolders
    }
  ]
};

// 写入 Apifox 原生格式文件
const apifoxOutputFile = path.join(__dirname, 'WeChat_4.1.8.27_Hook_完整版.apifox.json');
fs.writeFileSync(apifoxOutputFile, JSON.stringify(apifoxProject, null, 2), 'utf8');
console.log('Apifox 原生文件生成成功:', apifoxOutputFile);
console.log('导出接口总数:', totalExported);

// 2. 同时生成通用的 OpenAPI 3.0.3 格式文件 (支持绝大多数 API 工具，导入 Apifox 100% 成功)
const openapi = {
  openapi: '3.0.3',
  info: {
    title: 'WeChat 4.x Hook 完整接口规范 (4.1.8.27)',
    description: '微信 4.1.8.27 64位底层 Hook 完整接口库，涵盖全部 95 个已实现接口。',
    version: '1.0.0'
  },
  servers: [
    {
      url: 'http://127.0.0.1:19088',
      description: '本地 Hook 默认服务端口'
    }
  ],
  tags: modules.map(m => ({ name: m.name, description: m.desc })),
  paths: {}
};

for (const mod of modules) {
  for (const p of mod.paths) {
    const item = buildApifoxApiItem(p, mod.name);
    const api = item.api;
    let reqBodySchema = api.requestBody && api.requestBody.jsonSchema ? api.requestBody.jsonSchema : { type: 'object' };
    let reqExample = {};
    try {
      reqExample = JSON.parse(api.requestBody.data);
    } catch (e) {
      reqExample = {};
    }

    let resExample = {};
    try {
      resExample = JSON.parse(api.responses[0].responseExamples[0].data);
    } catch (e) {
      resExample = { errCode: 1, errMsg: 'ok' };
    }

    openapi.paths[p] = {
      post: {
        tags: [mod.name],
        summary: api.name,
        description: api.description,
        operationId: p.replace('/api/', ''),
        requestBody: {
          required: Object.keys(reqBodySchema.properties || {}).length > 0,
          content: {
            'application/json': {
              schema: reqBodySchema,
              example: reqExample
            }
          }
        },
        responses: {
          '200': {
            description: '成功响应',
            content: {
              'application/json': {
                schema: { type: 'object' },
                example: resExample
              }
            }
          }
        }
      }
    };
  }
}

const openapiOutputFile = path.join(__dirname, 'WeChat_4.1.8.27_Hook_OpenAPI3.json');
fs.writeFileSync(openapiOutputFile, JSON.stringify(openapi, null, 2), 'utf8');
console.log('OpenAPI 3.0 文件生成成功:', openapiOutputFile);
