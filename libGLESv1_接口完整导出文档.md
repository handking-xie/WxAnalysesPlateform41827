# libGLESv1.dll (WeChat 4.1.8.27 Hook) 接口完整导出文档

> **文档说明**：
> 本文档基于逆向分析提取自核心注入动态库 [libGLESv1.dll](file:///d:/WxHookSource41827/libGLESv1.dll)，并交叉校验了 [新版4.x_hook文档.apifox.json](file:///d:/WxHookSource41827/%E6%96%B0%E7%89%884.x_hook%E6%96%87%E6%A1%A3.apifox.json)。
> 覆盖了底层注册并完整实现的全部 **95 个 API 接口**（包含原 Apifox 文档中遗漏或拼写错误的 35+ 个隐藏接口，如好友个人朋友圈、朋友圈点赞/评论、红包拆领、好友添加/验证等）。
> 
> - **默认服务端口**：`19088`
> - **默认主机地址**：`http://127.0.0.1:19088`
> - **数据通信方式**：HTTP 请求触发 + Webhook / WebSocket 异步事件回调

---

## 目录索引 (Index)

### 一、消息发送与交互模块 (Messages)
- [1. 发送文本消息 Copy (`/api/send_text_msg`)](#1-%E5%8F%91%E9%80%81%E6%96%87%E6%9C%AC%E6%B6%88%E6%81%AFcopy)
- [2. 发送AT消息 (`/api/send_at_text`)](#2-%E5%8F%91%E9%80%81at%E6%B6%88%E6%81%AF)
- [3. 发送图片消息 (`/api/send_image_msg`)](#3-%E5%8F%91%E9%80%81%E5%9B%BE%E7%89%87%E6%B6%88%E6%81%AF)
- [4. 发送语音消息 (`/api/send_voice`)](#4-%E5%8F%91%E9%80%81%E8%AF%AD%E9%9F%B3%E6%B6%88%E6%81%AF)
- [5. 发送MP3语音 (`/api/send_mp3_voice`)](#5-%E5%8F%91%E9%80%81mp3%E8%AF%AD%E9%9F%B3)
- [6. 发送文件消息 (`/api/send_file_msg`)](#6-%E5%8F%91%E9%80%81%E6%96%87%E4%BB%B6%E6%B6%88%E6%81%AF)
- [7. 发送名片消息 (`/api/send_card_msg`)](#7-%E5%8F%91%E9%80%81%E5%90%8D%E7%89%87%E6%B6%88%E6%81%AF)
- [8. 发送本地GIF信息 (`/api/send_emotion_msg`)](#8-%E5%8F%91%E9%80%81%E6%9C%AC%E5%9C%B0gif%E4%BF%A1%E6%81%AF)
- [9. 发送链接信息_不走UI (`/api/send_xml`)](#9-%E5%8F%91%E9%80%81%E9%93%BE%E6%8E%A5%E4%BF%A1%E6%81%AF_%E4%B8%8D%E8%B5%B0ui)
- [10. 发送原始 App 消息 XML (`/api/send_app_xml`)](#10-%E5%8F%91%E9%80%81%E5%8E%9F%E5%A7%8Bapp%E6%B6%88%E6%81%AFxml)
- [11. 发送卡片/XML消息 (`/api/send_app_msg`)](#11-%E5%8F%91%E9%80%81%E5%8D%A1%E7%89%87xml%E6%B6%88%E6%81%AF)
- [12. 撤回任何消息 (`/api/revoke_msg`)](#12-%E6%92%A4%E5%9B%9E%E4%BB%BB%E4%BD%95%E6%B6%88%E6%81%AF)

### 二、朋友圈模块 (Moments / SNS)
- [13. 获取朋友圈首页 (`/api/sns_get_first_page`)](#13-%E8%8E%B7%E5%8F%96%E6%9C%8B%E5%8F%8B%E5%9C%88%E9%A6%96%E9%A1%B5)
- [14. 获取朋友圈下一页 (`/api/sns_get_next_page`)](#14-%E8%8E%B7%E5%8F%96%E6%9C%8B%E5%8F%8B%E5%9C%88%E4%B8%8B%E4%B8%80%E9%A1%B5)
- [15. 获取朋友圈详情 (`/api/sns_get_detail`)](#15-%E8%8E%B7%E5%8F%96%E6%9C%8B%E5%8F%8B%E5%9C%88%E8%AF%A6%E6%83%85)
- [16. 发送朋友圈 (`/api/sns_post`)](#16-%E5%8F%91%E9%80%81%E6%9C%8B%E5%8F%8B%E5%9C%88)
- [17. 发送图片朋友圈 (`/api/sns_send_img`)](#17-%E5%8F%91%E9%80%81%E5%9B%BE%E7%89%87%E6%9C%8B%E5%8F%8B%E5%9C%88)
- [18. 发送 XML 自定义朋友圈 (`/api/sns_send_xml`)](#18-%E5%8F%91%E9%80%81xml%E8%87%AA%E5%AE%9A%E4%B9%89%E6%9C%8B%E5%8F%8B%E5%9C%88)
- [19. 朋友圈点赞 (`/api/sns_like`)](#19-%E6%9C%8B%E5%8F%8B%E5%9C%88%E7%82%B9%E8%B5%9E)
- [20. 取消朋友圈点赞 (`/api/sns_unlike`)](#20-%E5%8F%96%E6%B6%88%E6%9C%8B%E5%8F%8B%E5%9C%88%E7%82%B9%E8%B5%9E)
- [21. 发表朋友圈评论 (`/api/sns_do_comment`)](#21-%E5%8F%91%E8%A1%A8%E6%9C%8B%E5%8F%8B%E5%9C%88%E8%AF%84%E8%AE%BA)
- [22. 朋友圈回复 (`/api/sns_comment_reply`)](#22-%E6%9C%8B%E5%8F%8B%E5%9C%88%E5%9B%9E%E5%A4%8D)
- [23. 删除朋友圈评论 (`/api/sns_del_comment`)](#23-%E5%88%A0%E9%99%A4%E6%9C%8B%E5%8F%8B%E5%9C%88%E8%AF%84%E8%AE%BA)
- [24. 删除朋友圈 (`/api/sns_del`)](#24-%E5%88%A0%E9%99%A4%E6%9C%8B%E5%8F%8B%E5%9C%88)
- [25. 朋友圈图片上传 (`/api/sns_upload`)](#25-%E6%9C%8B%E5%8F%8B%E5%9C%88%E5%9B%BE%E7%89%87%E4%B8%8A%E4%BC%A0)

### 三、联系人与好友管理模块 (Contacts & Friends)
- [26. 获取好友资料(网络获取) (`/api/get_contact_list`)](#26-%E8%8E%B7%E5%8F%96%E5%A5%BD%E5%8F%8B%E8%B5%84%E6%96%99%E7%BD%91%E7%BB%9C%E8%8E%B7%E5%8F%96)
- [27. 快速查找好友资料(非常快) (`/api/get_contact_fast`)](#27-%E5%BF%AB%E9%80%9F%E6%9F%A5%E6%89%BE%E5%A5%BD%E5%8F%8B%E8%B5%84%E6%96%99%E9%9D%9E%E5%B8%B8%E5%BF%AB)
- [28. 更新单个用户资料 (`/api/update_contact`)](#28-%E6%9B%B4%E6%96%B0%E5%8D%95%E4%B8%AA%E7%94%A8%E6%88%B7%E8%B5%84%E6%96%99)
- [29. 更新好友列表 (`/api/update_all_friend`)](#29-%E6%9B%B4%E6%96%B0%E5%A5%BD%E5%8F%8B%E5%88%97%E8%A1%A8)
- [30. 获取好友资料(网络获取) (`/api/get_realfriend_list`)](#30-%E8%8E%B7%E5%8F%96%E5%A5%BD%E5%8F%8B%E8%B5%84%E6%96%99%E7%BD%91%E7%BB%9C%E8%8E%B7%E5%8F%96)
- [31. 获取好友列表 (`/api/get_frien_lists`)](#31-%E8%8E%B7%E5%8F%96%E5%A5%BD%E5%8F%8B%E5%88%97%E8%A1%A8)
- [32. 搜索微信号/手机号 (`/api/net_scene_search_contact`)](#32-%E6%90%9C%E7%B4%A2%E5%BE%AE%E4%BF%A1%E5%8F%B7%E6%89%8B%E6%9C%BA%E5%8F%B7)
- [33. 获取个人最新网络 (`/api/get_profile_new`)](#33-%E8%8E%B7%E5%8F%96%E4%B8%AA%E4%BA%BA%E6%9C%80%E6%96%B0%E7%BD%91%E7%BB%9C)
- [34. 获取用户昵称与备注 (`/api/get_user_nick`)](#34-%E8%8E%B7%E5%8F%96%E7%94%A8%E6%88%B7%E6%98%B5%E7%A7%B0%E4%B8%8E%E5%A4%87%E6%B3%A8)
- [35. 主动添加好友 (`/api/add_friend`)](#35-%E4%B8%BB%E5%8A%A8%E6%B7%BB%E5%8A%A0%E5%A5%BD%E5%8F%8B)
- [36. 验证好友请求 (`/api/verify_friend`)](#36-%E9%AA%8C%E8%AF%81%E5%A5%BD%E5%8F%8B%E8%AF%B7%E6%B1%82)
- [37. 通过好友验证申请 (`/api/verify_user`)](#37-%E9%80%9A%E8%BF%87%E5%A5%BD%E5%8F%8B%E9%AA%8C%E8%AF%81%E7%94%B3%E8%AF%B7)
- [38. 修改好友备注 (`/api/remark_contact`)](#38-%E4%BF%AE%E6%94%B9%E5%A5%BD%E5%8F%8B%E5%A4%87%E6%B3%A8)
- [39. 删除好友 (`/api/del_contact`)](#39-%E5%88%A0%E9%99%A4%E5%A5%BD%E5%8F%8B)
- [40. 修改自己昵称 (`/api/mod_self_nick_name`)](#40-%E4%BF%AE%E6%94%B9%E8%87%AA%E5%B7%B1%E6%98%B5%E7%A7%B0)
- [41. 修改个人签名 (`/api/mod_self_nick_signature`)](#41-%E4%BF%AE%E6%94%B9%E4%B8%AA%E4%BA%BA%E7%AD%BE%E5%90%8D)
- [42. 修改头像 (`/api/upload_head_img`)](#42-%E4%BF%AE%E6%94%B9%E5%A4%B4%E5%83%8F)
- [43. 获取好友二维码 (`/api/get_my_qrocde`)](#43-%E8%8E%B7%E5%8F%96%E5%A5%BD%E5%8F%8B%E4%BA%8C%E7%BB%B4%E7%A0%81)
- [44. 获取附近人 (`/api/get_lbs_friend`)](#44-%E8%8E%B7%E5%8F%96%E9%99%84%E8%BF%91%E4%BA%BA)
- [45. 获取好友资料(网络获取) (`/api/get_gh_list`)](#45-%E8%8E%B7%E5%8F%96%E5%A5%BD%E5%8F%8B%E8%B5%84%E6%96%99%E7%BD%91%E7%BB%9C%E8%8E%B7%E5%8F%96)

### 四、群聊管理模块 (Chatrooms)
- [46. 创建群聊 (`/api/creat_chat_room`)](#46-%E5%88%9B%E5%BB%BA%E7%BE%A4%E8%81%8A)
- [47. 添加群成员40人以内 (`/api/add_member_to_chat_room`)](#47-%E6%B7%BB%E5%8A%A0%E7%BE%A4%E6%88%90%E5%91%9840%E4%BA%BA%E4%BB%A5%E5%86%85)
- [48. 邀请进入群聊 (`/api/invite_member_to_chat_room`)](#48-%E9%82%80%E8%AF%B7%E8%BF%9B%E5%85%A5%E7%BE%A4%E8%81%8A)
- [49. 踢出群成员 (`/api/del_member_from_chat_room`)](#49-%E8%B8%A2%E5%87%BA%E7%BE%A4%E6%88%90%E5%91%98)
- [50. 退出群聊 (`/api/quit_and_del_chat_room`)](#50-%E9%80%80%E5%87%BA%E7%BE%A4%E8%81%8A)
- [51. 获取群聊列表 (`/api/get_chatroom_list`)](#51-%E8%8E%B7%E5%8F%96%E7%BE%A4%E8%81%8A%E5%88%97%E8%A1%A8)
- [52. 获取群详情缓存 (`/api/get_chatroom_detail`)](#52-%E8%8E%B7%E5%8F%96%E7%BE%A4%E8%AF%A6%E6%83%85%E7%BC%93%E5%AD%98)
- [53. 获取群成员列表 (`/api/bm_get_room_members`)](#53-%E8%8E%B7%E5%8F%96%E7%BE%A4%E6%88%90%E5%91%98%E5%88%97%E8%A1%A8)
- [54. 获取群员昵称 (`/api/get_group_memeber_info`)](#54-%E8%8E%B7%E5%8F%96%E7%BE%A4%E5%91%98%E6%98%B5%E7%A7%B0)
- [55. 更新群成员联系人信息 (`/api/update_group_member_contact`)](#55-%E6%9B%B4%E6%96%B0%E7%BE%A4%E6%88%90%E5%91%98%E8%81%94%E7%B3%BB%E4%BA%BA%E4%BF%A1%E6%81%AF)
- [56. 添加群管理 (`/api/set_room_admin`)](#56-%E6%B7%BB%E5%8A%A0%E7%BE%A4%E7%AE%A1%E7%90%86)
- [57. 删除群管理 (`/api/del_room_admin`)](#57-%E5%88%A0%E9%99%A4%E7%BE%A4%E7%AE%A1%E7%90%86)
- [58. 修改群名称 (`/api/mod_chat_room_name`)](#58-%E4%BF%AE%E6%94%B9%E7%BE%A4%E5%90%8D%E7%A7%B0)
- [59. 修改我所在群的群昵称 (`/api/mod_chat_room_self_nick_name`)](#59-%E4%BF%AE%E6%94%B9%E6%88%91%E6%89%80%E5%9C%A8%E7%BE%A4%E7%9A%84%E7%BE%A4%E6%98%B5%E7%A7%B0)
- [60. 获取群成员简要信息(获取群成员昵称接口) (`/api/bm_member_nick`)](#60-%E8%8E%B7%E5%8F%96%E7%BE%A4%E6%88%90%E5%91%98%E7%AE%80%E8%A6%81%E4%BF%A1%E6%81%AF%E8%8E%B7%E5%8F%96%E7%BE%A4%E6%88%90%E5%91%98%E6%98%B5%E7%A7%B0%E6%8E%A5%E5%8F%A3)
- [61. 获取群成员简要信息(获取群成员昵称接口) (`/api/show_member_nick`)](#61-%E8%8E%B7%E5%8F%96%E7%BE%A4%E6%88%90%E5%91%98%E7%AE%80%E8%A6%81%E4%BF%A1%E6%81%AF%E8%8E%B7%E5%8F%96%E7%BE%A4%E6%88%90%E5%91%98%E6%98%B5%E7%A7%B0%E6%8E%A5%E5%8F%A3)
- [62. 设置群公告 (`/api/set_room_announcement_pb`)](#62-%E8%AE%BE%E7%BD%AE%E7%BE%A4%E5%85%AC%E5%91%8A)
- [63. 转让群主 (`/api/transferchatroomowner`)](#63-%E8%BD%AC%E8%AE%A9%E7%BE%A4%E4%B8%BB)
- [64. 同意群聊邀请 (`/api/enter_room`)](#64-%E5%90%8C%E6%84%8F%E7%BE%A4%E8%81%8A%E9%82%80%E8%AF%B7)
- [65. 保存群聊到通讯录 (`/api/save_chatroom_to_contact`)](#65-%E4%BF%9D%E5%AD%98%E7%BE%A4%E8%81%8A%E5%88%B0%E9%80%9A%E8%AE%AF%E5%BD%95)
- [66. 移除群聊通讯录 (`/api/remov_chatroom_to_contact`)](#66-%E7%A7%BB%E9%99%A4%E7%BE%A4%E8%81%8A%E9%80%9A%E8%AE%AF%E5%BD%95)
- [67. 获取群聊链接 A8Key (`/api/get_room_a8key`)](#67-%E8%8E%B7%E5%8F%96%E7%BE%A4%E8%81%8A%E9%93%BE%E6%8E%A5a8key)
- [68. 查询群成员信息 (`/api/net_scene_get_member_from_chat_room`)](#68-%E6%9F%A5%E8%AF%A2%E7%BE%A4%E6%88%90%E5%91%98%E4%BF%A1%E6%81%AF)

### 五、标签管理模块 (Labels)
- [69. 获取标签列表 (`/api/get_label_lists`)](#69-%E8%8E%B7%E5%8F%96%E6%A0%87%E7%AD%BE%E5%88%97%E8%A1%A8)
- [70. 增加标签 (`/api/add_label`)](#70-%E5%A2%9E%E5%8A%A0%E6%A0%87%E7%AD%BE)
- [71. 删除标签 (`/api/del_label`)](#71-%E5%88%A0%E9%99%A4%E6%A0%87%E7%AD%BE)
- [72. 修改好友标签 (`/api/modify_contact_label`)](#72-%E4%BF%AE%E6%94%B9%E5%A5%BD%E5%8F%8B%E6%A0%87%E7%AD%BE)
- [73. 根据标签获取好友列表 (`/api/get_friend_by_labelId`)](#73-%E6%A0%B9%E6%8D%AE%E6%A0%87%E7%AD%BE%E8%8E%B7%E5%8F%96%E5%A5%BD%E5%8F%8B%E5%88%97%E8%A1%A8)

### 六、多媒体与资源下载模块 (Media & Downloads)
- [74. 下载图片 (`/api/download_img`)](#74-%E4%B8%8B%E8%BD%BD%E5%9B%BE%E7%89%87)
- [75. 下载视频 (`/api/download_video`)](#75-%E4%B8%8B%E8%BD%BD%E8%A7%86%E9%A2%91)
- [76. 下载文件 (`/api/download_file`)](#76-%E4%B8%8B%E8%BD%BD%E6%96%87%E4%BB%B6)
- [77. 下载语音 (`/api/download_voice`)](#77-%E4%B8%8B%E8%BD%BD%E8%AF%AD%E9%9F%B3)
- [78. 语音消息转文字 (`/api/get_voice_trans`)](#78-%E8%AF%AD%E9%9F%B3%E6%B6%88%E6%81%AF%E8%BD%AC%E6%96%87%E5%AD%97)
- [79. 获取配置文件保存目录 (`/api/get_config_path`)](#79-%E8%8E%B7%E5%8F%96%E9%85%8D%E7%BD%AE%E6%96%87%E4%BB%B6%E4%BF%9D%E5%AD%98%E7%9B%AE%E5%BD%95)

### 七、微信支付与红包模块 (Pay & RedPackets)
- [80. 生成个人收款/支付二维码 (`/api/generate_payqrcode`)](#80-%E7%94%9F%E6%88%90%E4%B8%AA%E4%BA%BA%E6%94%B6%E6%AC%BE%E6%94%AF%E4%BB%98%E4%BA%8C%E7%BB%B4%E7%A0%81)
- [81. 确认收款 (`/api/ten_pay_trans_fer_confirm`)](#81-%E7%A1%AE%E8%AE%A4%E6%94%B6%E6%AC%BE)
- [82. 拒绝收款 (`/api/un_ten_pay_trans_fer_confirm`)](#82-%E6%8B%92%E7%BB%9D%E6%94%B6%E6%AC%BE)
- [83. 接收/查看微信红包 (`/api/receivewxhb`)](#83-%E6%8E%A5%E6%94%B6%E6%9F%A5%E7%9C%8B%E5%BE%AE%E4%BF%A1%E7%BA%A2%E5%8C%85)
- [84. 打开微信红包 (拆红包) (`/api/openwxhb`)](#84-%E6%89%93%E5%BC%80%E5%BE%AE%E4%BF%A1%E7%BA%A2%E5%8C%85%E6%8B%86%E7%BA%A2%E5%8C%85)
- [85. 打开定制红包 (VIP通道) (`/api/openwxhb_vip`)](#85-%E6%89%93%E5%BC%80%E5%AE%9A%E5%88%B6%E7%BA%A2%E5%8C%85vip%E9%80%9A%E9%81%93)

### 八、系统与底层数据库模块 (System & Database)
- [86. 微信初始化_删除当前设备_慎用 (`/api/wechat_init`)](#86-%E5%BE%AE%E4%BF%A1%E5%88%9D%E5%A7%8B%E5%8C%96_%E5%88%A0%E9%99%A4%E5%BD%93%E5%89%8D%E8%AE%BE%E5%A4%87_%E6%85%8E%E7%94%A8)
- [87. 接口连通性测试 (`/api/test`)](#87-%E6%8E%A5%E5%8F%A3%E8%BF%9E%E9%80%9A%E6%80%A7%E6%B5%8B%E8%AF%95)
- [88. 防撤回 (`/api/anti_revoke`)](#88-%E9%98%B2%E6%92%A4%E5%9B%9E)
- [89. 获取A8key (`/api/get_a8key`)](#89-%E8%8E%B7%E5%8F%96a8key)
- [90. 获取小程序code (`/api/js_login`)](#90-%E8%8E%B7%E5%8F%96%E5%B0%8F%E7%A8%8B%E5%BA%8Fcode)
- [91. 自动登录微信 (`/api/auto_login`)](#91-%E8%87%AA%E5%8A%A8%E7%99%BB%E5%BD%95%E5%BE%AE%E4%BF%A1)
- [92. 获取数据库句柄 (`/api/get_db_handle`)](#92-%E8%8E%B7%E5%8F%96%E6%95%B0%E6%8D%AE%E5%BA%93%E5%8F%A5%E6%9F%84)
- [93. 执行数据库查询 (`/api/sqlite3_exec`)](#93-%E6%89%A7%E8%A1%8C%E6%95%B0%E6%8D%AE%E5%BA%93%E6%9F%A5%E8%AF%A2)
- [94. 获取收藏列表 (`/api/get_fav_list`)](#94-%E8%8E%B7%E5%8F%96%E6%94%B6%E8%97%8F%E5%88%97%E8%A1%A8)

---

## 接口详细规范

## 一、消息发送与交互模块 (Messages)

> 包含向好友或群聊发送文本、@群成员、图片、语音、MP3音频、文件、卡片、表情、XML 以及消息撤回等接口。

### 1. 发送文本消息 Copy (`/api/send_text_msg`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_text_msg`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "45220347292@chatroom",
  "msg": "6666666666"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `msg` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "code": 1,
    "data": null,
    "msg": "success"
}
```

---
### 2. 发送AT消息 (`/api/send_at_text`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_at_text`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxids": "notify@all",
  "msg": "@爱吃香菜 123123",
  "roomId": "48520920817@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `msg` | `string` |  (必填) |
| `wxids` | `string` |  (必填) |
| `roomId` | `string` | notify@all为所有人 (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "code": 1,
    "data": null,
    "msg": "success"
}
```

---
### 3. 发送图片消息 (`/api/send_image_msg`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_image_msg`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "50210378945@chatroom",
  "filepath": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABkAAAAZCAYAAADE6YVjAAAAAXNSR0IArs4c6QAAAcNJREFUSEu9lTtLA0EQx2dyFpY2amlSCYJ+hDywNSDYXDoDFnb5AAqJoB/Azi6xyjWCcLZyl5SWCoKVl1JtLG2SlVlyybqP270gWbhm53Z+8/jPLsISFi6BAbkg9aF/TEGxMSQrAMl9LUhcgrRCuGOGbQAoKg4RYgB2G5aDXhYsE1IfNiJgULVGixB7Y9Y0ZWaEOAPSCDJAWsjBwG8jYEfOYGN1nW99/nxpk2PAOg+V4EI2KpDDyC+OC/gu/kjO9zfL0Ng6mm33R3dAn7y8CSvJZVMguiyu9s5hd21HcUgZnTy1/u4ja8pCsEIog9b2qbH3Z8+X8PL9Orcz1gurQVM8oEDkhhOAQKallA0hDsv9Wi4I9UHshQx7/BjC9dvNfNsJEvtdQOSTTYt6QT0xLblcOoVpysUnvCs6NTWeekEQcTlB6EB90CAJz64RWcKkKiqTRsJJWOmXrHNCP5iG0VizqcF5GFNHi1wrsqpSX9kXpFQ2YyYaRWVKWDROr5hIe83Pw1TmwqknOUDaRueGcCFEfhULXNbiw+UEoPPWlzGNaqo4GlICJd6E1f7t+RVT5xl5ULQ9twuVyzYfNrtzuWyOsuy/L17DGkVC6sUAAAAASUVORK5CYII=",
  "image_path": ""
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `image_path` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "code": 1,
    "data": null,
    "msg": "success"
}
```

---
### 4. 发送语音消息 (`/api/send_voice`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_voice`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "toWxid": "wxid_n629zjvs2ath29",
  "silkPath": "E:\\project_java\\wechat_ai_english_evaluation\\voice\\SILK_202512230312900.silk"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `toWxid` | `string` |  (必填) |
| `silkPath` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "toUserName": "wxid_8543785438012",
    "offset": 0,
    "length": 10483,
    "createTime": 1755645384,
    "clientMsgId": "JzAxzrj5BlDIC8omNElOfPrcDFK4QBopDYq422FMFtDYFvorepassuL9Ky",
    "msgId": 0,
    "voiceLength": 5000,
    "endFlag": 1,
    "baseResponse": {
        "ret": -2,
        "errMsg": {}
    },
    "cancelFlag": 0,
    "newMsgId": "8047442179716189588",
    "actionFlag": 0
}
```

---
### 5. 发送MP3语音 (`/api/send_mp3_voice`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_mp3_voice`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_n629zjvs2ath29",
  "mp3Path": "E:\\project_java\\wechat_ai_english_evaluation\\voice\\output.mp3"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `mp3Path` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 6. 发送文件消息 (`/api/send_file_msg`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_file_msg`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "48520920817@chatroom",
  "filepath": "D:\\谷歌浏览器下载\\SteamSetup.exe"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `filepath` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "code": 1,
    "data": null,
    "info": "请勿使用二手贩子贩卖的成品,售后无人处理 请联系作者购买",
    "msg": "success"
}
```

---
### 7. 发送名片消息 (`/api/send_card_msg`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_card_msg`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "towxid": "filehelper",
  "fromwxid": "wxid_8543785438012",
  "01K42J7JY0B9QV64Q8M3GM314Z": ""
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `towxid` | `string` | 谁的名片 (必填) |
| `fromwxid` | `string` |  (必填) |
| `01K42J7JY0B9QV64Q8M3GM314Z` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "count": 1,
    "list": [
        {
            "ret": 0,
            "toUserName": {
                "String": "filehelper"
            },
            "msgId": 0,
            "clientMsgId": 533085137,
            "createTime": 1756728024,
            "serverTime": 1756728030,
            "type": 42,
            "newMsgId": "6369419172447763745"
        }
    ],
    "actionFlag": 0
}
```

---
### 8. 发送本地GIF信息 (`/api/send_emotion_msg`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_emotion_msg`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "75",
  "filepath": "/var/tmp"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `filepath` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 9. 发送链接信息_不走UI (`/api/send_xml`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_xml`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "63",
  "title": "由于向下更加全啊",
  "description": "再业最到。难族开常因团象后也。可命保口。行几布族道市打传段程。实看属处总义合色强明。们第运一型加始。周学金会劳。",
  "thumbUrl": "https://gleaming-nougat.org/",
  "url": "https://soggy-daddy.org/"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `title` | `string` |  (必填) |
| `description` | `string` |  (必填) |
| `thumbUrl` | `string` |  (必填) |
| `url` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 10. 发送原始 App 消息 XML (`/api/send_app_xml`)

> **功能说明**：向好友或群聊发送原始构造的 App 消息 (如合并聊天记录、文件卡片、图文分享等)

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_app_xml`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "wxid_target",
  "xml": "<msg><appmsg appid=\"\">...</appmsg></msg>"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` | 接收者微信 ID 或群聊 ID |
| `xml` | `string` | App 消息的原始 XML 数据 |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回 `{"errCode": 1}`，发送成功。

---
### 11. 发送卡片/XML消息 (`/api/send_app_msg`)

> **功能说明**：该接口所有的卡片信息都可以发 包括但不限于 小程序 位置 音乐卡片等等

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/send_app_msg`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "content": "<appmsg appid=\"\" sdkver=\"\"><title>霜尘与#跟你爹的聊天记录</title><des>#年 轻人:[图片]&#x0D;&#x0A;#年轻人:[图片]&#x0D;&#x0A;</des><action>view</action><type>19</type><showtype>0</showtype><content></content><url>http://support.weixin.qq.com/cgi-bin/mmsupport-bin/readtemplate?t=page/favorite_record__w_unsupport</url><dataurl></dataurl><lowurl></lowurl><lowdataurl></lowdataurl><recorditem>&lt;recordinfo&gt;&lt;title&gt;霜尘与#年轻人的聊天记录&lt;/title&gt;&lt;desc&gt;#年轻人:[图片]&#x0D;&#x0A;#年轻人:[图片]&#x0D;&#x0A;&lt;/desc&gt;&lt;datalist count=&quot;2&quot;&gt;&lt;dataitem dataid=&quot;4e96646c705feed69d136026ce1f2a10&quot; datatype=&quot;2&quot; datasourceid=&quot;2551522912052390538&quot;&gt;&lt;messageuuid&gt;31f58b6996c6b292bff26b4190019f51_&lt;/messageuuid&gt;&lt;cdndataurl&gt;3057020100044b304902010002041e03640802032f57270204b480ac27020468172819042439396266386430352d663634332d346565382d396234342d3532633734363433373538360204059420010201000405004c55cf00&lt;/cdndataurl&gt;&lt;cdnencryver&gt;1&lt;/cdnencryver&gt;&lt;cdnthumburl&gt;3057020100044b304902010002041e03640802032f57270204b480ac27020468172819042437336434363630332d313934622d343561622d623863632d3265636330653662633065350204059420010201000405004c55cf00&lt;/cdnthumburl&gt;&lt;datafmt&gt;jpg&lt;/datafmt&gt;&lt;fullmd5&gt;a0ea895b8083b2b687bbc099ecaa1c0d&lt;/fullmd5&gt;&lt;datasize&gt;299187&lt;/datasize&gt;&lt;head256md5&gt;de5bc6f58e2351897a63eac28cb05dde&lt;/head256md5&gt;&lt;thumbfullmd5&gt;b3b410b9423e893e4f39e2cbcd4efb36&lt;/thumbfullmd5&gt;&lt;thumbsize&gt;3544&lt;/thumbsize&gt;&lt;thumbhead256md5&gt;97e74287ec5d2166bf9205f81075be88&lt;/thumbhead256md5&gt;&lt;sourcedatapath /&gt;&lt;sourcethumbpath /&gt;&lt;msgDataPath /&gt;&lt;msgThumpPath /&gt;&lt;sourcename&gt;#年轻人&lt;/sourcename&gt;&lt;sourcetime&gt;2020-04-21 11:02:04&lt;/sourcetime&gt;&lt;cdnthumbkey&gt;970d36d8cbff9bf041db4a11b87a9348&lt;/cdnthumbkey&gt;&lt;cdndatakey&gt;a64c5e7f575cdc58a57a340e32d0f8ea&lt;/cdndatakey&gt;&lt;dataitemsource&gt;&lt;fromusr&gt;wxid_mfcw4upkx6vm22&lt;/fromusr&gt;&lt;tousr&gt;wxid_ozyqateb85un22&lt;/tousr&gt;&lt;msgid&gt;2551522912052390538&lt;/msgid&gt;&lt;/dataitemsource&gt;&lt;/dataitem&gt;&lt;dataitem dataid=&quot;0d1c893e58674e9a963d9a77969367a4&quot; datatype=&quot;2&quot; datasourceid=&quot;1536673819017551849&quot;&gt;&lt;messageuuid&gt;34bf9006db0efc5fcfa4fa8a9f8de5e7_&lt;/messageuuid&gt;&lt;cdndataurl&gt;3057020100044b304902010002041e03640802032f57270204b480ac2702046817281a042461346630616134352d396563312d343035622d396138392d3462363461323337313531300204059420010201000405004c50bb00&lt;/cdndataurl&gt;&lt;cdnencryver&gt;1&lt;/cdnencryver&gt;&lt;cdnthumburl&gt;3057020100044b304902010002041e03640802032f57270204b480ac2702046817281a042432616132623239332d346536342d343531642d616634342d6239343737363437333461640204059420010201000405004c53db00&lt;/cdnthumburl&gt;&lt;datafmt&gt;jpg&lt;/datafmt&gt;&lt;fullmd5&gt;9587fc2946a1029d4924f59f249a9731&lt;/fullmd5&gt;&lt;datasize&gt;290904&lt;/datasize&gt;&lt;head256md5&gt;de5bc6f58e2351897a63eac28cb05dde&lt;/head256md5&gt;&lt;thumbfullmd5&gt;140e8bf2b466d9fd9d3ab28be71452ad&lt;/thumbfullmd5&gt;&lt;thumbsize&gt;2966&lt;/thumbsize&gt;&lt;thumbhead256md5&gt;d3c028f682e4f0add4c4e8bb7442ea20&lt;/thumbhead256md5&gt;&lt;sourcedatapath /&gt;&lt;sourcethumbpath /&gt;&lt;msgDataPath /&gt;&lt;msgThumpPath /&gt;&lt;sourcename&gt;#年轻 人&lt;/sourcename&gt;&lt;sourcetime&gt;2020-04-21 11:03:37&lt;/sourcetime&gt;&lt;cdnthumbkey&gt;fc18d9304d636050e51f462c032099df&lt;/cdnthumbkey&gt;&lt;cdndatakey&gt;ce3e39132a609ed5318127d49213f84f&lt;/cdndatakey&gt;&lt;dataitemsource&gt;&lt;fromusr&gt;wxid_mfcw4upkx6vm22&lt;/fromusr&gt;&lt;tousr&gt;wxid_ozyqateb85un22&lt;/tousr&gt;&lt;msgid&gt;1536673819017551849&lt;/msgid&gt;&lt;/dataitemsource&gt;&lt;/dataitem&gt;&lt;/datalist&gt;&lt;favusername&gt;&lt;/favusername&gt;&lt;favcreatetime&gt;0&lt;/favcreatetime&gt;&lt;/recordinfo&gt;</recorditem><thumburl></thumburl><messageaction></messageaction><laninfo></laninfo><extinfo></extinfo><sourceusername></sourceusername><sourcedisplayname></sourcedisplayname><commenturl></commenturl><appattach><totallen>0</totallen><attachid></attachid><emoticonmd5></emoticonmd5><fileext></fileext><aeskey></aeskey></appattach><webviewshared><publisherId></publisherId><publisherReqId>0</publisherReqId></webviewshared><weappinfo><pagepath></pagepath><username></username><appid></appid><appservicetype>0</appservicetype></weappinfo><websearch /></appmsg>",
  "type": "19",
  "wxid": "filehelper"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `content` | `string` |  (必填) |
| `type` | `string` |  (必填) |
| `wxid` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 12. 撤回任何消息 (`/api/revoke_msg`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/revoke_msg`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "newMsgId": 2050044161371926300,
  "createTime": 1761391928,
  "toUserName": "49767299448@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `newMsgId` | `number` |  (必填) |
| `createTime` | `integer` |  (必填) |
| `toUserName` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "account_wxid": "string",
    "data": {
        "baseResponse": {
            "errMsg": {},
            "ret": 0
        },
        "sysWording": "string"
    },
    "errCode": 0,
    "errMsg": "string"
}
```

---
## 二、朋友圈模块 (Moments / SNS)

> 包含发布朋友圈、朋友圈首页/下一页时间线抓取、好友个人朋友圈获取、朋友圈详情、点赞、评论与删除等全功能接口。

### 13. 获取朋友圈首页 (`/api/sns_get_first_page`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_get_first_page`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "firstPageMd5": "string",
  "maxId": "string"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `firstPageMd5` | `string` | 第一页的md5如果没有可以为空 (必填) |
| `maxId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 14. 获取朋友圈下一页 (`/api/sns_get_next_page`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_get_next_page`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "lastItemid": "14689529228577936097"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `lastItemid` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 15. 获取朋友圈详情 (`/api/sns_get_detail`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_get_detail`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "sns_id": 14420282581074719000
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `sns_id` | `number` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 16. 发送朋友圈 (`/api/sns_post`)

> **功能说明**：这个发朋友圈 容易掉线 说实话不太建议用

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_post`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "content": "6666666666666666666666",
  "blackList": "",
  "withauserList": ""
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `content` | `string` | 朋友圈文本 (必填) |
| `blackList` | `string` | 黑名单列表 (必填) |
| `withauserList` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 17. 发送图片朋友圈 (`/api/sns_send_img`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_send_img`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "filelist": "D:\\7777777.jpg",
  "content": "测试文字朋友圈"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `filelist` | `string` | 图片文件列表 (必填) |
| `content` | `string` | 文本内容 (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 18. 发送 XML 自定义朋友圈 (`/api/sns_send_xml`)

> **功能说明**：通过原始 XML 格式直接构造并发布朋友圈内容（支持图文、视频、音乐等富媒体卡片）

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_send_xml`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "xml": "<TimelineObject><id>0</id><contentDesc>测试自定义朋友圈</contentDesc>...</TimelineObject>"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `xml` | `string` | 标准完整的 TimelineObject 朋友圈 XML 字符串 |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回 `{"errCode": 1}`。朋友圈发布成功后，服务器会推送该朋友圈的真实 SNS ID。

---
### 19. 朋友圈点赞 (`/api/sns_like`)

> **功能说明**：对指定朋友圈动态执行点赞操作

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_like`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "sns_id": "14420282581074719279",
  "to_wxid": "wxid_owner"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `sns_id` | `string` | 目标朋友圈的动态 ID |
| `to_wxid` | `string` | 发布该朋友圈的作者微信 ID |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回 `{"errCode": 1}`，点赞操作成功。

---
### 20. 取消朋友圈点赞 (`/api/sns_unlike`)

> **功能说明**：取消对某条朋友圈的赞

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_unlike`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "sns_id": "14420282581074719279",
  "to_wxid": "wxid_owner"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `sns_id` | `string` | 目标朋友圈的动态 ID |
| `to_wxid` | `string` | 发布该朋友圈的作者微信 ID |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回 `{"errCode": 1}`，取消点赞成功。

---
### 21. 发表朋友圈评论 (`/api/sns_do_comment`)

> **功能说明**：在指定朋友圈动态下直接发表一级评论

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_do_comment`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "sns_id": "14420282581074719279",
  "comment": "评论内容",
  "to_wxid": "wxid_owner"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `sns_id` | `string` | 目标朋友圈的动态 ID |
| `comment` | `string` | 评论文本内容 |
| `to_wxid` | `string` | 发布该朋友圈的作者微信 ID |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回 `{"errCode": 1}` 及新生成的评论 ID。

---
### 22. 朋友圈回复 (`/api/sns_comment_reply`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_comment_reply`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "content": "66666666666",
  "sns_id": "14667428703163265648",
  "comment_id": 3
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `content` | `string` | 回复内容 (必填) |
| `sns_id` | `string` | 朋友圈id (必填) |
| `comment_id` | `integer` | 评论id (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 23. 删除朋友圈评论 (`/api/sns_del_comment`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_del_comment`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "sns_id": "14661929784229180031",
  "commentId": "3"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `sns_id` | `string` | 朋友圈id (必填) |
| `commentId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 24. 删除朋友圈 (`/api/sns_del`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_del`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "sns_id": "14667428703163265648"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `sns_id` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 25. 朋友圈图片上传 (`/api/sns_upload`)

> **功能说明**：该接口取的url其实可以当做图库

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_upload`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "filePath": "D:\\1.jpg"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `filePath` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
## 三、联系人与好友管理模块 (Contacts & Friends)

> 包含好友信息查询、好友添加、验证申请、备注修改、删除好友、修改个人信息、公众号列表等接口。

### 26. 获取好友资料(网络获取) (`/api/get_contact_list`)

> **功能说明**：该接口网络获取好友最新资料

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_contact_list`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_rj8cjqdrg5cl22"
}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "contactCount": 1,
    "contactList": [
        {
            "userName": {
                "String": "filehelper"
            },
            "nickName": {
                "String": "文件传输助手"
            },
            "pyinitial": {
                "String": "WJCSZS"
            },
            "quanPin": {
                "String": "wenjianchuanshuzhushou"
            },
            "sex": 0,
            "imgBuf": {
                "iLen": 0
            },
            "bitMask": 4294967295,
            "bitVal": 3,
            "imgFlag": 3,
            "remark": {},
            "remarkPyinitial": {},
            "remarkQuanPin": {},
            "contactType": 0,
            "roomInfoCount": 0,
            "domainList": {},
            "chatRoomNotify": 0,
            "addContactScene": 0,
            "personalCard": 0,
            "hasWeiXinHdHeadImg": 1,
            "verifyFlag": 0,
            "level": 0,
            "source": 6,
            "weiboFlag": 0,
            "albumStyle": 0,
            "albumFlag": 0,
            "snsUserInfo": {
                "snsFlag": 0,
                "snsBgobjectId": "0",
                "snsFlagEx": 16,
                "snsPrivacyRecent": 0
            },
            "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/0",
            "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/132",
            "customizedInfo": {
                "brandFlag": 0
            },
            "headImgMd5": "860baf36d77682daa9ce1210be61374e",
            "encryptUserName": "v3_020b3826fd03010000000000283d02027bc00e000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fdb3566c7e0f6dd453b321e7729cd107b0e7f2e215e554ae6e2e881d8a917e9584@stranger",
            "additionalContactList": {
                "linkedinContactItem": {}
            },
            "chatroomVersion": 0,
            "chatroomMaxCount": 0,
            "chatroomAccessType": 0,
            "newChatroomData": {
                "memberCount": 0,
                "infoMask": 0,
                "chatRoomUserName": {},
                "watchMemberCount": 0
            },
            "deleteFlag": 0,
            "phoneNumListInfo": {
                "count": 0
            },
            "chatroomInfoVersion": 0,
            "deleteContactScene": 0,
            "chatroomStatus": 0,
            "extFlag": 0,
            "chatRoomBusinessType": "0",
            "friendUserName": "filehelper",
            "textStatusFlag": 2,
            "ringBackSetting": {
                "finderObjectId": "0",
                "startTs": 0,
                "endTs": 0
            },
            "bitMask2": "18446744073709551615",
            "bitValue2": "0",
            "contactExtraInfoBuf": {
                "iLen": 0
            },
            "isInChatRoom": 0,
            "eraseChatRoomMemberData": 0
        }
    ],
    "ret": [
        0
    ],
    "verifyUserValidTicketList": {
        "username": "filehelper",
        "antispamticket": "v4_000b708f0b0400000100000000007216f55900af00be97e0d58baf681000000050ded0b020927e3c97896a09d47e6e9e23b2464fed6bdfd91729d2159eef78ffea979d110f34e73a4d6d1247cc360645720f1e8928b6cb80404c08635111878eeafc925805736f6382dc8cc062d71929b3878d61500db77779d534021191ba6b6aaeab78f8357452@stranger"
    }
}
```

---
### 27. 快速查找好友资料(非常快) (`/api/get_contact_fast`)

> **功能说明**：建议使用fast接口 最好去更新一下好友资料 之后需要频繁查询速度就会变得非常快 
更新好友列表 只需要更新一次即可 后面重新登录获取好友资料也非常快

建议第一次使用的时候 wechat_init  调用该接口更新一下 该接口为长耗时接口 具体速度根据你好友数量来算

或者也可以只更新单个好友资料 

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_contact_fast`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_rdpo01enuad821"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "contact": {
        "alias": "",
        "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/0",
        "bitMask": 4294967295,
        "bitVal": 3,
        "city": "",
        "country": "",
        "encryptUserName": "v3_020b3826fd03010000000000283d02027bc00e000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fdb3566c7e0f6dd453b321e7729cd107b0e7f2e215e554ae6e2e881d8a917e9584@stranger",
        "hasWeiXinHdHeadImg": 1,
        "imgBuf": {
            "buffer": "",
            "iLen": 0
        },
        "imgFlag": 2,
        "nickName": {
            "String": "文件传输助手"
        },
        "province": "",
        "pyinitial": {
            "String": "WJCSZS"
        },
        "quanPin": {
            "String": "wenjianchuanshuzhushou"
        },
        "remark": {
            "String": ""
        },
        "remarkPyinitial": {
            "String": ""
        },
        "remarkQuanPin": {
            "String": ""
        },
        "sex": 0,
        "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/132",
        "snsUserInfo": {
            "snsFlag": 0
        },
        "textStatusExtInfo": "",
        "textStatusFlag": 2,
        "textStatusId": "",
        "userName": {
            "String": "filehelper"
        },
        "verifyFlag": 0
    },
    "ret": 0,
    "username": "filehelper"
}
```

---
### 28. 更新单个用户资料 (`/api/update_contact`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/update_contact`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "49866796771@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "alias": "",
    "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UtLAESIsbSVID3ms1eg1oTvjUNTLWS7qiceImgk7jUAm6Y7PZ7icTsuEoyHibvXLFd5AxkSK30M8agHicSl0Z9kUFWIrvfvxLpBBx3nDV1hClV6FZiaksQegnJfK9welYDqfj/0",
    "bitMask": 4294967295,
    "bitVal": 1,
    "city": "",
    "country": "",
    "encryptUserName": "v3_020b3826fd03010000000000ca8402b5500f2c000000501ea9a3dba12f95f6b60a0536a1adb6b42927056065fce45dd36cbd2be7e1ec6ac3334980b27d1fa6952c948e721935a76b34b6aa3b57e6b5dcf90dc2@stranger",
    "hasWeiXinHdHeadImg": 1,
    "imgBuf": {
        "buffer": "",
        "iLen": 0
    },
    "imgFlag": 2,
    "nickName": {
        "String": "文件传输助手"
    },
    "province": "",
    "pyinitial": {
        "String": "WJCSZS"
    },
    "quanPin": {
        "String": "wenjianchuanshuzhushou"
    },
    "remark": {
        "String": ""
    },
    "remarkPyinitial": {
        "String": ""
    },
    "remarkQuanPin": {
        "String": ""
    },
    "sex": 0,
    "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UtLAESIsbSVID3ms1eg1oTvjUNTLWS7qiceImgk7jUAm6Y7PZ7icTsuEoyHibvXLFd5AxkSK30M8agHicSl0Z9kUFWIrvfvxLpBBx3nDV1hClV6FZiaksQegnJfK9welYDqfj/132",
    "snsUserInfo": {
        "snsFlag": 0
    },
    "textStatusExtInfo": "",
    "textStatusFlag": 2,
    "textStatusId": "",
    "userName": {
        "String": "filehelper"
    },
    "verifyFlag": 0
}
```

---
### 29. 更新好友列表 (`/api/update_all_friend`)

> **功能说明**：该接口会向微信服务器获取一次完整的好友列表 该接口跟 快速查找好友资料联动
建议一天更新一次就好 如果你好友长时间没有变化 不更新也行

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/update_all_friend`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "data": [
        {
            "contact": {
                "alias": "string",
                "bigHeadImgUrl": "string",
                "bitMask": 0,
                "bitVal": 0,
                "city": "string",
                "country": "string",
                "encryptUserName": "string",
                "hasWeiXinHdHeadImg": 0,
                "imgBuf": {
                    "buffer": "string",
                    "iLen": 0
                },
                "imgFlag": 0,
                "nickName": {
                    "String": "string"
                },
                "province": "string",
                "pyinitial": {
                    "String": "string"
                },
                "quanPin": {
                    "String": "string"
                },
                "remark": {
                    "String": "string"
                },
                "remarkPyinitial": {
                    "String": "string"
                },
                "remarkQuanPin": {
                    "String": "string"
                },
                "sex": 0,
                "smallHeadImgUrl": "string",
                "snsUserInfo": {
                    "snsFlag": 0
                },
                "textStatusFlag": 0,
                "userName": {
                    "String": "string"
                },
                "verifyFlag": 0,
                "customizedInfo": {
                    "brandFlag": 0,
                    "brandIconUrl": "string",
                    "externalInfo": "string"
                },
                "description": "string",
                "labelIdlist": "string",
                "phoneNumListInfo": {
                    "count": 0
                },
                "textStatusExtInfo": "string",
                "textStatusId": "string",
                "contactType": 0,
                "deleteFlag": 0,
                "chatroomVersion": 0
            },
            "ret": 0,
            "username": "string"
        }
    ],
    "friend_count": 0
}
```

---
### 30. 获取好友资料(网络获取) (`/api/get_realfriend_list`)

> **功能说明**：该接口网络获取好友最新资料

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_realfriend_list`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_rj8cjqdrg5cl22"
}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "contactCount": 1,
    "contactList": [
        {
            "userName": {
                "String": "filehelper"
            },
            "nickName": {
                "String": "文件传输助手"
            },
            "pyinitial": {
                "String": "WJCSZS"
            },
            "quanPin": {
                "String": "wenjianchuanshuzhushou"
            },
            "sex": 0,
            "imgBuf": {
                "iLen": 0
            },
            "bitMask": 4294967295,
            "bitVal": 3,
            "imgFlag": 3,
            "remark": {},
            "remarkPyinitial": {},
            "remarkQuanPin": {},
            "contactType": 0,
            "roomInfoCount": 0,
            "domainList": {},
            "chatRoomNotify": 0,
            "addContactScene": 0,
            "personalCard": 0,
            "hasWeiXinHdHeadImg": 1,
            "verifyFlag": 0,
            "level": 0,
            "source": 6,
            "weiboFlag": 0,
            "albumStyle": 0,
            "albumFlag": 0,
            "snsUserInfo": {
                "snsFlag": 0,
                "snsBgobjectId": "0",
                "snsFlagEx": 16,
                "snsPrivacyRecent": 0
            },
            "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/0",
            "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/132",
            "customizedInfo": {
                "brandFlag": 0
            },
            "headImgMd5": "860baf36d77682daa9ce1210be61374e",
            "encryptUserName": "v3_020b3826fd03010000000000283d02027bc00e000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fdb3566c7e0f6dd453b321e7729cd107b0e7f2e215e554ae6e2e881d8a917e9584@stranger",
            "additionalContactList": {
                "linkedinContactItem": {}
            },
            "chatroomVersion": 0,
            "chatroomMaxCount": 0,
            "chatroomAccessType": 0,
            "newChatroomData": {
                "memberCount": 0,
                "infoMask": 0,
                "chatRoomUserName": {},
                "watchMemberCount": 0
            },
            "deleteFlag": 0,
            "phoneNumListInfo": {
                "count": 0
            },
            "chatroomInfoVersion": 0,
            "deleteContactScene": 0,
            "chatroomStatus": 0,
            "extFlag": 0,
            "chatRoomBusinessType": "0",
            "friendUserName": "filehelper",
            "textStatusFlag": 2,
            "ringBackSetting": {
                "finderObjectId": "0",
                "startTs": 0,
                "endTs": 0
            },
            "bitMask2": "18446744073709551615",
            "bitValue2": "0",
            "contactExtraInfoBuf": {
                "iLen": 0
            },
            "isInChatRoom": 0,
            "eraseChatRoomMemberData": 0
        }
    ],
    "ret": [
        0
    ],
    "verifyUserValidTicketList": {
        "username": "filehelper",
        "antispamticket": "v4_000b708f0b0400000100000000007216f55900af00be97e0d58baf681000000050ded0b020927e3c97896a09d47e6e9e23b2464fed6bdfd91729d2159eef78ffea979d110f34e73a4d6d1247cc360645720f1e8928b6cb80404c08635111878eeafc925805736f6382dc8cc062d71929b3878d61500db77779d534021191ba6b6aaeab78f8357452@stranger"
    }
}
```

---
### 31. 获取好友列表 (`/api/get_frien_lists`)

> **功能说明**：该接口第一次获取要从网络拿到一次好友列表 之后会以kv存到本地 速度非常的快

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_frien_lists`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "data": [
        {
            "contact": {
                "alias": "",
                "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/JHHDzT07V5jP78o6NSXyMUd8ZbgJG4wH5nx44Rq1vftIUXmSFDtiasicJ18rXN3MU980bLMQ48f9IUnCXPia4W7KQ/0",
                "bitMask": 4294967295,
                "bitVal": 1,
                "city": "",
                "country": "",
                "encryptUserName": "v3_020b3826fd0301000000000034bc2a9383cc9c000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fd0e1424ea5070b3d30d80aa895e783f48f451d0cf5e2aa4f8cd7d29d450c977f9@stranger",
                "hasWeiXinHdHeadImg": 1,
                "imgBuf": {
                    "buffer": "",
                    "iLen": 0
                },
                "imgFlag": 2,
                "nickName": {
                    "String": "语音记事本"
                },
                "province": "",
                "pyinitial": {
                    "String": "YYJSB"
                },
                "quanPin": {
                    "String": "yuyinjishiben"
                },
                "remark": {
                    "String": ""
                },
                "remarkPyinitial": {
                    "String": ""
                },
                "remarkQuanPin": {
                    "String": ""
                },
                "sex": 0,
                "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/JHHDzT07V5jP78o6NSXyMUd8ZbgJG4wH5nx44Rq1vftIUXmSFDtiasicJ18rXN3MU980bLMQ48f9IUnCXPia4W7KQ/132",
                "snsUserInfo": {
                    "snsFlag": 0
                },
                "textStatusFlag": 2,
                "userName": {
                    "String": "medianote"
                },
                "verifyFlag": 0
            },
            "ret": 0,
            "username": "medianote"
        },
        {
            "contact": {
                "alias": "",
                "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/Q3auHgzwzM6H8bJKHKyGY2mk0ljLfodkWnrRbXLn3P11f68cg0ePxA/0",
                "bitMask": 4294967295,
                "bitVal": 1,
                "city": "",
                "country": "",
                "customizedInfo": {
                    "brandFlag": 0,
                    "brandIconUrl": "http://mmbiz.qpic.cn/mmbiz_png/aOncaiaP23TAocUWWAAXgfmq5oZq5Qz9tFXIsMEnnkk2JtRWlfnh34oMUJ72s0KcfHhGIM7mwXVpslXoBnWWKkg/0?wx_fmt=png",
                    "externalInfo": "{\"IsShowHeadImgInMsg\":\"1\",\"IsHideInputToolbarInMsg\":\"0\",\"IsAgreeProtocol\":\"1\",\"RoleId\":\"1\",\"InteractiveMode\":\"2\",\"VerifySource\":{\"Description\":\"深圳市腾讯计算机系统有限公司\",\"IntroUrl\":\"http:\/\/mp.weixin.qq.com\/mp\/getverifyinfo?__biz=MTAwMDA1#wechat_webview_type=1&wechat_redirect\",\"Type\":0,\"VerifyBizType\":1,\"VerifyCustomerType\":1},\"MMBizMenu\":{\"uin\":100005,\"interactive_mode\":2,\"update_time\":1664970762,\"button_list\":[{\"id\":3574535416,\"type\":0,\"name\":\"自助工具\",\"key\":\"rselfmenu_0\",\"value\":\"\",\"sub_button_list\":[{\"id\":3574535416,\"type\":2,\"name\":\"忘记密码\",\"key\":\"rselfmenu_0_0\",\"value\":\"https:\/\/support.weixin.qq.com\/getpassword?lang=zh_CN&pass_ticket=6Uo%2F3ydY35gDBzZI1ZKLPHIx3qEwkQDTHD55hScdu7U%3D\",\"sub_button_list\":[],\"native_url\":\"\"},{\"id\":3574535416,\"type\":2,\"name\":\"冻结帐号\",\"key\":\"rselfmenu_0_1\",\"value\":\"https:\/\/weixin110.qq.com\/freeze\",\"sub_button_list\":[],\"native_url\":\"\"},{\"id\":3574535416,\"type\":2,\"name\":\"解冻帐号\",\"key\":\"rselfmenu_0_2\",\"value\":\"https:\/\/weixin110.qq.com\/unfreeze\",\"sub_button_list\":[],\"native_url\":\"\"},{\"id\":3574535416,\"type\":2,\"name\":\"注册辅助验证\",\"key\":\"rselfmenu_0_3\",\"value\":\"https:\/\/weixin110.qq.com\/security\/readtemplate?t=signup_verify\/w_wxteam_help\",\"sub_button_list\":[],\"native_url\":\"\"},{\"id\":3574535416,\"type\":2,\"name\":\"解封\/申诉辅助验证\",\"key\":\"rselfmenu_0_4\",\"value\":\"https:\/\/weixin110.qq.com\/security\/readtemplate?t=w_security_center_website\/w_friend_help_request\",\"sub_button_list\":[],\"native_url\":\"\"}],\"native_url\":\"\"},{\"id\":3574535416,\"type\":2,\"name\":\"帮助与反馈\",\"key\":\"rselfmenu_1\",\"value\":\"https:\/\/kf.qq.com\/cgi-bin\/commh5jumpwx?jumpurl=https%3A%2F%2Fkf.qq.com%2Ftouch%2Fwechat-product%2Findex.html?scene=wxhelp\",\"sub_button_list\":[],\"native_url\":\"\"}],\"version\":3574535416},\"ScanQRCodeType\":1,\"ServiceType\":1,\"RegisterSource\":{\"RegisterBody\":\"深圳市腾讯计算机系统有限公司\",\"IntroUrl\":\"http:\/\/mp.weixin.qq.com\/mp\/getverifyinfo?__biz=MTAwMDA1&type=reg_info#wechat_redirect\",\"AboutBizUrl\":\"http:\/\/mp.weixin.qq.com\/mp\/aboutbiz?__biz=MTAwMDA1#wechat_redirect\"},\"Appid\":\"wx10583a7e974992ec\"}"
                },
                "encryptUserName": "v3_020b3826fd03010000000000b7701a593e1b44000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fdfad7660e940b41264abd9a3f4611c599796b646b66f6db50fb4b52b9312bc300@stranger",
                "hasWeiXinHdHeadImg": 1,
                "imgBuf": {
                    "buffer": "",
                    "iLen": 0
                },
                "imgFlag": 2,
                "nickName": {
                    "String": "微信团队"
                },
                "province": "",
                "pyinitial": {
                    "String": "WXTD"
                },
                "quanPin": {
                    "String": "weixintuandui"
                },
                "remark": {
                    "String": ""
                },
                "remarkPyinitial": {
                    "String": ""
                },
                "remarkQuanPin": {
                    "String": ""
                },
                "sex": 0,
                "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/Q3auHgzwzM6H8bJKHKyGY2mk0ljLfodkWnrRbXLn3P11f68cg0ePxA/132",
                "snsUserInfo": {
                    "snsFlag": 0
                },
                "textStatusFlag": 2,
                "userName": {
                    "String": "weixin"
                },
                "verifyFlag": 56
            },
            "ret": 0,
            "username": "weixin"
        }
    ],
    "friend_count": 147
}
```

---
### 32. 搜索微信号/手机号 (`/api/net_scene_search_contact`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/net_scene_search_contact`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "search": "搜索微信号还是手机号"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `search` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {
            "String": "Everything is OK"
        }
    },
    "userName": {
        "String": "v3_020b3826fd03010000000000c7f228b4f06efa000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fd5de96fe637ffb434b1d4fe0f451c904eb4aba33f1a4b8c976735c47abb45dd77e67209c666ce8e85fbb59d586e0157f8@stranger"
    },
    "nickName": {
        "String": "悬淼"
    },
    "pyinitial": {
        "String": "wxid_hify7vdpvg5d22"
    },
    "quanPin": {
        "String": "wxid_hify7vdpvg5d22"
    },
    "sex": 0,
    "imgBuf": {
        "iLen": 0
    },
    "signature": "天上天下，唯我独尊",
    "personalCard": 1,
    "verifyFlag": 0,
    "weiboFlag": 0,
    "albumStyle": 0,
    "albumFlag": 0,
    "snsUserInfo": {
        "snsFlag": 0,
        "snsBgobjectId": "0",
        "snsFlagEx": 0,
        "snsPrivacyRecent": 0
    },
    "customizedInfo": {
        "brandFlag": 0
    },
    "contactCount": 0,
    "bigHeadImgUrl": "http://wx.qlogo.cn/mmhead/ver_1/H9ukOUmCmkabkwXmfTbiaNZLuFpLKzgGSxaZ5IzY0pUPdCshNmuzwgFSLLDe2mZlNUKysKGaqefgWUFseqTFdoviaW6Sny7kQ09iaiaH5go8LyNqBJw7Lzh2AyWPms2MoKef/0",
    "smallHeadImgUrl": "http://wx.qlogo.cn/mmhead/ver_1/H9ukOUmCmkabkwXmfTbiaNZLuFpLKzgGSxaZ5IzY0pUPdCshNmuzwgFSLLDe2mZlNUKysKGaqefgWUFseqTFdoviaW6Sny7kQ09iaiaH5go8LyNqBJw7Lzh2AyWPms2MoKef/132",
    "resBuf": {
        "iLen": 0
    },
    "antispamTicket": "v4_000b708f0b04000001000000000029ac3dc68a17dea708381427a5681000000050ded0b020927e3c97896a09d47e6e9eb3fbad3b5aa09a6124b67addb011f45e340030f8d0743331300126b01af1cbba4a7ecbfa7a7dfe8d3622d0412a8243b323aa2eac8f59b61aeeaaaeb5c083362db9bc4ab949ef4c89557f289493d143ec1f216ad6648091cf@stranger",
    "matchType": 2,
    "extFlag": 0,
    "searchContactJumpInfo": {}
}
```

---
### 33. 获取个人最新网络 (`/api/get_profile_new`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_profile_new`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "userInfo": {
        "bitFlag": 190,
        "userName": {
            "String": "你的wxid"
        },
        "nickName": {
            "String": "隔壁老陈"
        },
        "bindUin": 0,
        "bindEmail": {},
        "bindMobile": {
            "String": "电话"
        },
        "status": 234021,
        "imgLen": 0,
        "sex": 2,
        "province": "省份",
        "city": "城市",
        "signature": "随便记住我 然后把我忘了吧",
        "personalCard": 1,
        "disturbSetting": {
            "nightSetting": 0,
            "nightTime": {
                "beginTime": 0,
                "endTime": 0
            },
            "allDaySetting": 0,
            "allDayTime": {
                "beginTime": 0,
                "endTime": 0
            }
        },
        "pluginFlag": 16939169,
        "verifyFlag": 0,
        "point": 478,
        "experience": 62,
        "level": 1,
        "levelLowExp": 0,
        "levelHighExp": 200,
        "pluginSwitch": 41984,
        "gmailList": {
            "count": 0
        },
        "alias": "hbbhcds",
        "weiboFlag": 0,
        "faceBookFlag": 0,
        "fbuserId": "0",
        "albumStyle": 0,
        "albumFlag": 0,
        "txnewsCategory": 0,
        "country": "CN"
    },
    "userInfoExt": {
        "snsUserInfo": {
            "snsFlag": 1,
            "snsBgimgId": "http://shmmsns.qpic.cn/mmsns/VT6V5OXuTMxYhxJetaAnqELiclpwsucyHFO7656Ds1ztTH25ZhuUvUibwNFLL2LBlha5rVp4picviaY/0",
            "snsBgobjectId": "13647912401971261663",
            "snsFlagEx": 7297,
            "snsPrivacyRecent": 72
        },
        "myBrandList": "****",
        "bigChatRoomSize": 0,
        "bigChatRoomQuota": 0,
        "bigChatRoomInvite": 0,
        "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkVyWXY1LFUEC0eUf9xGBptHhAoh7Yl7CsrTJQnZ8nlM0R58c/0",
        "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkVyWXY1LFUEC0eUf9xGBptHhAoh7Yl7CsrTJQnZ8nlM0R58c/132",
        "mainAcctType": 0,
        "extXml": {},
        "safeDeviceList": {
            "count": 5,
            "list": [
                {
                    "name": "设备名称",
                    "uuid": "设备uuid",
                    "deviceType": "android-33",
                    "createTime": 1666420216
                }
            ]
        },
        "safeDevice": 0,
        "grayscaleFlag": 359,
        "regCountry": "CN",
        "linkedinContactItem": {},
        "patternLockInfo": {
            "patternVersion": 7,
            "sign": {
                "iLen": 156,
                "buffer": "*****"
            },
            "lockStatus": 0
        },
        "payWalletType": 0,
        "walletRegion": 1,
        "extStatus": "****",
        "userStatus": 1,
        "paySetting": "1",
        "patSuffix": "的钱包说请你吃饭",
        "patSuffixVersion": 2,
        "teenagerModeFinderSetting": 1,
        "teenagerModeBizAcctSetting": 0,
        "teenagerModeMiniProgramSetting": 0,
        "xagreementInfo": {
            "funcsSwitch": "0",
            "funcsUserChoiceSwitch": "0"
        },
        "salt": "******",
        "finderSetting": "0",
        "ringBackSetting": {
            "finderObjectId": "0",
            "startTs": 0,
            "endTs": 0
        },
        "smcryptoFlag": 0,
        "globalRingBackSetting": {
            "type": 0,
            "startTime": 0,
            "endTime": 0,
            "music": {
                "sid": 0
            },
            "finder": {
                "finderObjectId": "0"
            }
        },
        "newcomeMsgDefaultVoiceNumber": 0,
        "discoveryPageCtrlFlag": "1",
        "extStatus2": "128",
        "finderLiveAliasSync": {
            "updateTime": "0",
            "spamFlag": 0,
            "deleteTime": "0"
        },
        "liveAliasRoleType": 1,
        "verifyContentList": {
            "count": 0
        },
        "lqtversion": 0,
        "teenagerModeEmotionSetting": 0,
        "notificationBannerDisplayContentSetting": 0
    }
}
```

---
### 34. 获取用户昵称与备注 (`/api/get_user_nick`)

> **功能说明**：获取指定联系人或群成员的昵称和备注

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_user_nick`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_xxxxxxxx"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` | 用户的微信 ID |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回用户昵称、备注、头像等详细对象。

---
### 35. 主动添加好友 (`/api/add_friend`)

> **功能说明**：向目标微信用户发送好友添加申请

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/add_friend`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_xxxxxxxx",
  "content": "您好，我是...",
  "scene": 15
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` | 目标用户的微信号或原始 wxid |
| `content` | `string` | 验证消息（申请添加附言） |
| `scene` | `number` | 添加来源场景（如 15:手机号搜索，30:扫一扫二维码，14:群聊等） |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回 `{"errCode": 1}`，申请发送成功。

---
### 36. 验证好友请求 (`/api/verify_friend`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/verify_friend`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "v3": "v3_020b3826fd030100000000005f2b2bd9947385000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fd8278657a4cf2987caea63020d2e155a519f9dfa519eab0af804b64eff6c022032dfd406af7e1acb60949bf368e977cc6@stranger",
  "v4": "v4_000b708f0b040000010000000000e688a70292f10fd5b0ea8e7dfa671000000050ded0b020927e3c97896a09d47e6e9eb1507dd07a6e2b5bcded893023cabade024616a6e2f66f233d7bd569d17097eccab67013d555ef0643d7808009487ea000460ff4d87fea808678bab1dc8cd008fc953bed97ca89932fcd123d14b2c7b017333846599fbaac9f1efedee4b72a36c5c1b77077d04d2c@stranger",
  "scene": "3",
  "scence": ""
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `v3` | `string` |  (必填) |
| `v4` | `string` |  (必填) |
| `scence` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 37. 通过好友验证申请 (`/api/verify_user`)

> **功能说明**：同意并通过收到的好友添加请求

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/verify_user`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "encryptUserName": "v1_xxxxxxxx@stranger",
  "ticket": "v2_xxxxxxxx",
  "scene": 6
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `encryptUserName` | `string` | 好友请求推送中的加密用户名 (v1_...) |
| `ticket` | `string` | 好友请求推送中的验证 ticket (v2_...) |
| `scene` | `number` | 添加来源场景值 |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回 `{"errCode": 1}`。通过后会触发好友关系变更推送与新好友首条欢迎消息。

---
### 38. 修改好友备注 (`/api/remark_contact`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/remark_contact`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_ozyqateb85un22",
  "remark": "111"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `remark` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 39. 删除好友 (`/api/del_contact`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/del_contact`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_8543785438012"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 40. 修改自己昵称 (`/api/mod_self_nick_name`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/mod_self_nick_name`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "newName": "鸭梨🍐大a"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `newName` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "ret": 0,
    "oplogRet": {
        "count": 1,
        "ret": [
            0
        ],
        "errMsg": [
            {}
        ]
    }
}
```

---
### 41. 修改个人签名 (`/api/mod_self_nick_signature`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/mod_self_nick_signature`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "newSignature": "666666666666666"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `newSignature` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 42. 修改头像 (`/api/upload_head_img`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/upload_head_img`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "filepath": "D:\\2.png"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `filepath` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 43. 获取好友二维码 (`/api/get_my_qrocde`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_my_qrocde`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "45220347292@chatroom",
  "opcode": "0",
  "style": "7",
  "info": "说明1-8 style都是风格 你们可以自己看看"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `opcode` | `string` |  (必填) |
| `style` | `string` |  (必填) |
| `info` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": -2,
        "errMsg": {}
    },
    "qrcode": {
        "iLen": 0
    },
    "style": 0,
    "dominatorColorSize": 0
}
```

---
### 44. 获取附近人 (`/api/get_lbs_friend`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_lbs_friend`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "longitude": "120.24646699999994",
  "latitude": "30.197153999999998"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `longitude` | `string` |  (必填) |
| `latitude` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": "返回码，0 表示成功",
        "errMsg": "错误信息"
    },
    "contactCount": "联系人数量",
    "contactList": [
        {
            "userName": "用户名（可能是加密后的唯一标识）",
            "nickName": "昵称",
            "province": "省份",
            "city": "城市",
            "signature": "个性签名",
            "distance": "距离（与自己的物理距离）",
            "sex": "性别（1=男，2=女，0=未知）",
            "imgStatus": "头像状态",
            "verifyFlag": "认证标志",
            "weiboFlag": "是否绑定微博",
            "headImgVersion": "头像版本号",
            "snsUserInfo": {
                "snsFlag": "朋友圈标志（是否开启朋友圈）",
                "snsBgimgId": "朋友圈背景图链接",
                "snsBgobjectId": "朋友圈背景图对象ID",
                "snsFlagEx": "朋友圈扩展标志位",
                "snsPrivacyRecent": "朋友圈隐私设置"
            },
            "country": "国家",
            "bigHeadImgUrl": "大头像 URL",
            "smallHeadImgUrl": "小头像 URL",
            "customizedInfo": {
                "brandFlag": "品牌标志（公众号/企业号相关）"
            },
            "antispamTicket": "防骚扰 ticket（陌生人校验用）",
            "flag": "标志位",
            "finderFlag": "视频号标志"
        }
    ],
    "state": "状态码",
    "flushTime": "刷新时间（秒）",
    "isShowRoom": "是否显示聊天室",
    "roomMemberCount": "聊天室成员数量"
}
```

---
### 45. 获取好友资料(网络获取) (`/api/get_gh_list`)

> **功能说明**：该接口网络获取好友最新资料

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_gh_list`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_rj8cjqdrg5cl22"
}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "contactCount": 1,
    "contactList": [
        {
            "userName": {
                "String": "filehelper"
            },
            "nickName": {
                "String": "文件传输助手"
            },
            "pyinitial": {
                "String": "WJCSZS"
            },
            "quanPin": {
                "String": "wenjianchuanshuzhushou"
            },
            "sex": 0,
            "imgBuf": {
                "iLen": 0
            },
            "bitMask": 4294967295,
            "bitVal": 3,
            "imgFlag": 3,
            "remark": {},
            "remarkPyinitial": {},
            "remarkQuanPin": {},
            "contactType": 0,
            "roomInfoCount": 0,
            "domainList": {},
            "chatRoomNotify": 0,
            "addContactScene": 0,
            "personalCard": 0,
            "hasWeiXinHdHeadImg": 1,
            "verifyFlag": 0,
            "level": 0,
            "source": 6,
            "weiboFlag": 0,
            "albumStyle": 0,
            "albumFlag": 0,
            "snsUserInfo": {
                "snsFlag": 0,
                "snsBgobjectId": "0",
                "snsFlagEx": 16,
                "snsPrivacyRecent": 0
            },
            "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/0",
            "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/132",
            "customizedInfo": {
                "brandFlag": 0
            },
            "headImgMd5": "860baf36d77682daa9ce1210be61374e",
            "encryptUserName": "v3_020b3826fd03010000000000283d02027bc00e000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fdb3566c7e0f6dd453b321e7729cd107b0e7f2e215e554ae6e2e881d8a917e9584@stranger",
            "additionalContactList": {
                "linkedinContactItem": {}
            },
            "chatroomVersion": 0,
            "chatroomMaxCount": 0,
            "chatroomAccessType": 0,
            "newChatroomData": {
                "memberCount": 0,
                "infoMask": 0,
                "chatRoomUserName": {},
                "watchMemberCount": 0
            },
            "deleteFlag": 0,
            "phoneNumListInfo": {
                "count": 0
            },
            "chatroomInfoVersion": 0,
            "deleteContactScene": 0,
            "chatroomStatus": 0,
            "extFlag": 0,
            "chatRoomBusinessType": "0",
            "friendUserName": "filehelper",
            "textStatusFlag": 2,
            "ringBackSetting": {
                "finderObjectId": "0",
                "startTs": 0,
                "endTs": 0
            },
            "bitMask2": "18446744073709551615",
            "bitValue2": "0",
            "contactExtraInfoBuf": {
                "iLen": 0
            },
            "isInChatRoom": 0,
            "eraseChatRoomMemberData": 0
        }
    ],
    "ret": [
        0
    ],
    "verifyUserValidTicketList": {
        "username": "filehelper",
        "antispamticket": "v4_000b708f0b0400000100000000007216f55900af00be97e0d58baf681000000050ded0b020927e3c97896a09d47e6e9e23b2464fed6bdfd91729d2159eef78ffea979d110f34e73a4d6d1247cc360645720f1e8928b6cb80404c08635111878eeafc925805736f6382dc8cc062d71929b3878d61500db77779d534021191ba6b6aaeab78f8357452@stranger"
    }
}
```

---
## 四、群聊管理模块 (Chatrooms)

> 包含创建群聊、邀请成员、踢出成员、退出群聊、设置管理员、修改群名、修改群内昵称、群公告等接口。

### 46. 创建群聊 (`/api/creat_chat_room`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/creat_chat_room`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxids": "wxid_3e9mll0g0fad21,wxid_8543785438012"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxids` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": -2,
        "errMsg": {
            "String": "<e>\n<ShowType>1</ShowType>\n<Content><![CDATA[创建群聊失败]]></Content>\n<Url><![CDATA[]]></Url>\n<DispSec>30</DispSec>\n<Title><![CDATA[]]></Title>\n<Action>4</Action>\n<DelayConnSec>0</DelayConnSec>\n<Countdown>0</Countdown>\n<Ok><![CDATA[]]></Ok>\n<Cancel><![CDATA[]]></Cancel>\n<Icon>0</Icon>\n</e>\n"
        }
    },
    "topic": {},
    "pyinitial": {},
    "quanPin": {},
    "memberCount": 0,
    "chatRoomName": {},
    "imgBuf": {
        "iLen": 0
    }
}
```

---
### 47. 添加群成员40人以内 (`/api/add_member_to_chat_room`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/add_member_to_chat_room`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid_list": "wxid_3e9mll0g0fad21",
  "room_id": "45220347292@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid_list` | `string` |  (必填) |
| `room_id` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 48. 邀请进入群聊 (`/api/invite_member_to_chat_room`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/invite_member_to_chat_room`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid_list": "wxid1,wxid2,wxid3",
  "room_id": "38994638667@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid_list` | `string` |  (必填) |
| `room_id` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 49. 踢出群成员 (`/api/del_member_from_chat_room`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/del_member_from_chat_room`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid_list": "wxid_8543785438012",
  "room_id": "49767299448@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid_list` | `string` |  (必填) |
| `room_id` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 50. 退出群聊 (`/api/quit_and_del_chat_room`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/quit_and_del_chat_room`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "xxxxxxxxxxxx"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 51. 获取群聊列表 (`/api/get_chatroom_list`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_chatroom_list`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 52. 获取群详情缓存 (`/api/get_chatroom_detail`)

> **功能说明**：该接口适用于 需要频繁获取群资料

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_chatroom_detail`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "43029636852@chatroom",
  "room_id": ""
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `room_id` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "chatroomUserName": "49767299448@chatroom",
    "serverVersion": 10004,
    "newChatroomData": {
        "memberCount": 2,
        "chatRoomMember": [
            {
                "userName": "wxid1",
                "nickName": "隔壁老陈",
                "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0NWrgPHXbiawI6vnZlV4hibibGvqb7hsTkr7fBYfO5Ss7LsksvF/0",
                "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0NWrgPHXbiawI6vnZlV4hibibGvqb7hsTkr7fBYfO5Ss7LsksvF/132",
                "chatroomMemberFlag": 1,
                "status": 0
            },
            {
                "userName": "wxid2",
                "nickName": "不必",
                "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UfAy94vgEmryCeyWxYAa1moicl0Tia1RnDzIDTHxxQZNKC7rjBtdRsezeL0B7sMicEicUILaxxic8QiazNlaDqRZD8vn2GrL4RIjhLuoAlfcPCPJLjQiaYe6ibn28oAdEwpsuh5W/0",
                "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UfAy94vgEmryCeyWxYAa1moicl0Tia1RnDzIDTHxxQZNKC7rjBtdRsezeL0B7sMicEicUILaxxic8QiazNlaDqRZD8vn2GrL4RIjhLuoAlfcPCPJLjQiaYe6ibn28oAdEwpsuh5W/132",
                "chatroomMemberFlag": 1,
                "inviterUserName": "wxid_ozyqateb85un22",
                "status": 0,
                "addChatRoomSceneNewXml": "<sysmsg type=\"ChatRoomMemberTraceBack\">\n\t<ChatRoomMemberTraceBack>\n\t\t<text><![CDATA[$inviter_username$邀请进群]]></text>\n\t\t<link>\n\t\t\t<username><![CDATA[wxid_ozyqateb85un22]]></username>\n\t\t</link>\n\t</ChatRoomMemberTraceBack>\n</sysmsg>\n"
            }
        ],
        "infoMask": 0,
        "chatRoomUserName": {},
        "watchMemberCount": 0
    },
    "chatRoomOwner": "wxid",
    "allMemberCount": 2,
    "allMemberUserNameList": [
        {
            "String": "wxid1"
        },
        {
            "String": "wxid2"
        }
    ],
    "adminCount": 0
}
```

---
### 53. 获取群成员列表 (`/api/bm_get_room_members`)

> **功能说明**：如果消息里面没有群成员资料的话 可以调用该接口进行缓存

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/bm_get_room_members`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "room_id": "49866796771@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `room_id` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "chatroomUserName": "49767299448@chatroom",
    "serverVersion": 10004,
    "newChatroomData": {
        "memberCount": 2,
        "chatRoomMember": [
            {
                "userName": "wxid1",
                "nickName": "隔壁老陈",
                "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0NWrgPHXbiawI6vnZlV4hibibGvqb7hsTkr7fBYfO5Ss7LsksvF/0",
                "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0NWrgPHXbiawI6vnZlV4hibibGvqb7hsTkr7fBYfO5Ss7LsksvF/132",
                "chatroomMemberFlag": 1,
                "status": 0
            },
            {
                "userName": "wxid2",
                "nickName": "不必",
                "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UfAy94vgEmryCeyWxYAa1moicl0Tia1RnDzIDTHxxQZNKC7rjBtdRsezeL0B7sMicEicUILaxxic8QiazNlaDqRZD8vn2GrL4RIjhLuoAlfcPCPJLjQiaYe6ibn28oAdEwpsuh5W/0",
                "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UfAy94vgEmryCeyWxYAa1moicl0Tia1RnDzIDTHxxQZNKC7rjBtdRsezeL0B7sMicEicUILaxxic8QiazNlaDqRZD8vn2GrL4RIjhLuoAlfcPCPJLjQiaYe6ibn28oAdEwpsuh5W/132",
                "chatroomMemberFlag": 1,
                "inviterUserName": "wxid_ozyqateb85un22",
                "status": 0,
                "addChatRoomSceneNewXml": "<sysmsg type=\"ChatRoomMemberTraceBack\">\n\t<ChatRoomMemberTraceBack>\n\t\t<text><![CDATA[$inviter_username$邀请进群]]></text>\n\t\t<link>\n\t\t\t<username><![CDATA[wxid_ozyqateb85un22]]></username>\n\t\t</link>\n\t</ChatRoomMemberTraceBack>\n</sysmsg>\n"
            }
        ],
        "infoMask": 0,
        "chatRoomUserName": {},
        "watchMemberCount": 0
    },
    "chatRoomOwner": "wxid",
    "allMemberCount": 2,
    "allMemberUserNameList": [
        {
            "String": "wxid1"
        },
        {
            "String": "wxid2"
        }
    ],
    "adminCount": 0
}
```

---
### 54. 获取群员昵称 (`/api/get_group_memeber_info`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_group_memeber_info`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "49767299448@chatroom",
  "memeberId": "wxid_bktzp6cv7wxe12"
}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 55. 更新群成员联系人信息 (`/api/update_group_member_contact`)

> **功能说明**：刷新指定群内某群成员的个人资料与联系人信息

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/update_group_member_contact`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "123456789@chatroom",
  "wxid": "wxid_xxxxxxxx"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` | 群聊 ID（以 @chatroom 结尾） |
| `wxid` | `string` | 群成员的微信 ID |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回 `{"errCode": 1}`。微信在后台向服务器请求更新联系人信息，更新完成后通过联系人更新事件推送。

---
### 56. 添加群管理 (`/api/set_room_admin`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/set_room_admin`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "49767299448@chatroom",
  "admin": "wxid_8543785438012"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` |  (必填) |
| `admin` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 57. 删除群管理 (`/api/del_room_admin`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/del_room_admin`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "49767299448@chatroom",
  "admin": "wxid_8543785438012"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` |  (必填) |
| `admin` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 58. 修改群名称 (`/api/mod_chat_room_name`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/mod_chat_room_name`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "49767299448@chatroom",
  "topic": "需要修改成的名称"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `topic` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 59. 修改我所在群的群昵称 (`/api/mod_chat_room_self_nick_name`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/mod_chat_room_self_nick_name`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "48520920817@chatroom",
  "nickName": "叭叭叭"
}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{"ret":0,"oplogRet":{"count":1,"ret":[0],"errMsg":[{}]}}
```

---
### 60. 获取群成员简要信息(获取群成员昵称接口) (`/api/bm_member_nick`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/bm_member_nick`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_3e9mll0g0fad21",
  "roomId": "49866796771@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `roomId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "account_wxid": "string",
    "data": {
        "addChatRoomSceneNewXml": "string",
        "bigHeadImgUrl": "string",
        "chatroomMemberFlag": 0,
        "inviterUserName": "string",
        "nickName": "string",
        "smallHeadImgUrl": "string",
        "status": 0,
        "userName": "string"
    },
    "errCode": 0,
    "errMsg": "string"
}
```

---
### 61. 获取群成员简要信息(获取群成员昵称接口) (`/api/show_member_nick`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/show_member_nick`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_3e9mll0g0fad21",
  "roomId": "49866796771@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `roomId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "account_wxid": "string",
    "data": {
        "addChatRoomSceneNewXml": "string",
        "bigHeadImgUrl": "string",
        "chatroomMemberFlag": 0,
        "inviterUserName": "string",
        "nickName": "string",
        "smallHeadImgUrl": "string",
        "status": 0,
        "userName": "string"
    },
    "errCode": 0,
    "errMsg": "string"
}
```

---
### 62. 设置群公告 (`/api/set_room_announcement_pb`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/set_room_announcement_pb`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "51687237616@chatroom",
  "announcement": "通知一下 下次别用之前的群公告版本了"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` |  (必填) |
| `announcement` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 63. 转让群主 (`/api/transferchatroomowner`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/transferchatroomowner`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "to_wxid": "string",
  "roomId": "string"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` |  (必填) |
| `to_wxid` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 64. 同意群聊邀请 (`/api/enter_room`)

> **功能说明**：运行该接口的时候 需要将url 用a8key转一下

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/enter_room`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "url": "https://support.weixin.qq.com/cgi-bin/mmsupport-bin/addopenimchatroombyinvite?ticket=BruwhiVyeJBslI7b"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `url` | `string` | 该url是获取a8key后的url (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 65. 保存群聊到通讯录 (`/api/save_chatroom_to_contact`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/save_chatroom_to_contact`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "群id"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 66. 移除群聊通讯录 (`/api/remov_chatroom_to_contact`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/remov_chatroom_to_contact`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "群id"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 67. 获取群聊链接 A8Key (`/api/get_room_a8key`)

> **功能说明**：请求群聊链接或群邀请的 A8Key 解析

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_room_a8key`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "roomId": "123456789@chatroom",
  "url": "https://weixin.qq.com/..."
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `roomId` | `string` | 群聊 ID |
| `url` | `string` | 需要解析的链接或邀请链接 |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回包含解析后 fullUrl / title / content 等相关信息的 JSON 数据。

---
### 68. 查询群成员信息 (`/api/net_scene_get_member_from_chat_room`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/net_scene_get_member_from_chat_room`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxid": "wxid_3e9mll0g0fad21",
  "roomId": "45220347292@chatroom"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxid` | `string` |  (必填) |
| `roomId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "contactCount": 1,
    "contactList": [
        {
            "userName": {
                "String": "群成员的wxid"
            },
            "nickName": {
                "String": "不必"
            },
            "pyinitial": {
                "String": "BB"
            },
            "quanPin": {
                "String": "bubi"
            },
            "sex": 1,
            "imgBuf": {
                "iLen": 0
            },
            "bitMask": 4294967295,
            "bitVal": 3,
            "imgFlag": 3,
            "remark": {
                "String": "9763"
            },
            "remarkPyinitial": {
                "String": "9763"
            },
            "remarkQuanPin": {
                "String": "9763"
            },
            "contactType": 0,
            "roomInfoCount": 0,
            "domainList": {},
            "chatRoomNotify": 0,
            "addContactScene": 0,
            "province": "Zhejiang",
            "city": "Hangzhou",
            "signature": "特别害怕失去很熟悉的人",
            "personalCard": 1,
            "hasWeiXinHdHeadImg": 1,
            "verifyFlag": 0,
            "level": 0,
            "source": 3,
            "alias": "jryswygq",
            "weiboFlag": 0,
            "albumStyle": 0,
            "albumFlag": 0,
            "snsUserInfo": {
                "snsFlag": 1,
                "snsBgimgId": "http://shmmsns.qpic.cn/mmsns/qcKhiayu3sNlcQLCwMDHfX38h9o7pCHkLtgBam5F6IgeABvibBTTib1bXiaVjCPZzEYTtVsbvian0EIk/0",
                "snsBgobjectId": "14693141287014765172",
                "snsFlagEx": 7297,
                "snsPrivacyRecent": 72
            },
            "country": "CN",
            "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/hic1c1goZbfmqcPfk2UllbdDGA5TC4ZwB7uINxase77pCZX2OU2MicGBw1ia3jBHKLPnbcSoySrCfsul8DjQBwAAAyPTA5Th5yibtZNBuxZhR8KIWvRqTGXNEQgticdEF61SX/0",
            "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/hic1c1goZbfmqcPfk2UllbdDGA5TC4ZwB7uINxase77pCZX2OU2MicGBw1ia3jBHKLPnbcSoySrCfsul8DjQBwAAAyPTA5Th5yibtZNBuxZhR8KIWvRqTGXNEQgticdEF61SX/132",
            "myBrandList": "<brandlist></brandlist>",
            "customizedInfo": {
                "brandFlag": 0
            },
            "headImgMd5": "00a7b20ff61ed9356a1221a6e265134d",
            "encryptUserName": "V3",
            "additionalContactList": {
                "linkedinContactItem": {}
            },
            "chatroomVersion": 0,
            "chatroomMaxCount": 0,
            "chatroomAccessType": 0,
            "newChatroomData": {
                "memberCount": 0,
                "infoMask": 0,
                "chatRoomUserName": {
                    "String": "49767299448@chatroom"
                },
                "watchMemberCount": 0
            },
            "deleteFlag": 0,
            "phoneNumListInfo": {
                "count": 0
            },
            "chatroomInfoVersion": 0,
            "deleteContactScene": 0,
            "chatroomStatus": 0,
            "extFlag": 0,
            "chatRoomBusinessType": "0",
            "friendUserName": "群成员的wxid",
            "textStatusFlag": 2,
            "ringBackSetting": {
                "finderObjectId": "0",
                "startTs": 0,
                "endTs": 0
            },
            "bitMask2": "18446744073709551615",
            "bitValue2": "256",
            "contactExtraInfoBuf": {
                "iLen": 0
            },
            "isInChatRoom": 0,
            "eraseChatRoomMemberData": 0
        }
    ],
    "ret": [
        0
    ],
    "verifyUserValidTicketList": {
        "username": "群成员的wxid",
        "antispamticket": "V4"
    }
}
```

---
## 五、标签管理模块 (Labels)

> 包含微信标签列表查询、创建标签、删除标签、为联系人打标签以及按标签查好友等接口。

### 69. 获取标签列表 (`/api/get_label_lists`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_label_lists`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "labelCount": 7,
    "labelPairList": [
        {
            "labelName": "取完钱",
            "labelId": 2
        },
        {
            "labelName": "1",
            "labelId": 7
        },
        {
            "labelName": "2",
            "labelId": 8
        },
        {
            "labelName": "777777777777",
            "labelId": 9
        },
        {
            "labelName": "标签名字6667",
            "labelId": 11
        },
        {
            "labelName": "6666666666666666",
            "labelId": 6
        },
        {
            "labelName": "15454454545",
            "labelId": 10
        }
    ]
}
```

---
### 70. 增加标签 (`/api/add_label`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/add_label`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "label": "标签名字"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `label` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "baseResponse": {
        "ret": 0,
        "errMsg": {}
    },
    "labelCount": 1,
    "labelPairList": [
        {
            "labelName": "我的标签",
            "labelId": 12
        }
    ]
}
```

---
### 71. 删除标签 (`/api/del_label`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/del_label`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "label_id": "33"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `label_id` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 72. 修改好友标签 (`/api/modify_contact_label`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/modify_contact_label`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "wxids": "wxid_8543785438012",
  "labelId": "2,6"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `wxids` | `string` |  (必填) |
| `labelId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 73. 根据标签获取好友列表 (`/api/get_friend_by_labelId`)

> **功能说明**：根据指定的标签 ID 查询该标签下的所有好友微信 ID 列表

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_friend_by_labelId`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "label": "12345"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `label` | `string` | 微信标签 ID（字符串或整型） |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回包含该标签下所有好友 wxid 列表的数组。

---
## 六、多媒体与资源下载模块 (Media & Downloads)

> 包含下载高清图片、下载视频文件、下载通用文件、下载语音文件、语音转文字等接口。

### 74. 下载图片 (`/api/download_img`)

> **功能说明**：这些参数都来自于消息里

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/download_img`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "to_user": "wxid_bdk9jdwumfgj22",
  "from_user": "wxid_n629zjvs2ath29",
  "start_pos": 0,
  "total_len": 559726,
  "data_len": 559726,
  "compress_type": 0,
  "MsgId": 1469066920,
  "path": "F:\\wechat_img\\7878787878.jpg"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `to_user` | `string` |  (必填) |
| `from_user` | `string` |  (必填) |
| `start_pos` | `integer` |  (必填) |
| `total_len` | `integer` |  (必填) |
| `data_len` | `integer` |  (必填) |
| `compress_type` | `integer` |  (必填) |
| `MsgId` | `integer` |  (必填) |
| `path` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "CompressType": 0,
    "FromUserName": "wxid_8543785438012",
    "MsgId": 1213935352,
    "ToUserName": "wxid_ozyqateb85un22",
    "TotalLen": 44041,
    "downloaded_bytes": 44041,
    "path": "d:\\7878787878.jpg",
    "status": "success"
}
```

---
### 75. 下载视频 (`/api/download_video`)

> **功能说明**：该接口里的所有数据均来自消息回调

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/download_video`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "total_len": 68264760,
  "NewMsgId": 150973212120165920,
  "path": "d:\\121.mp4",
  "MsgId": 425004325
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `total_len` | `integer` |  (必填) |
| `NewMsgId` | `number` |  (必填) |
| `path` | `string` |  (必填) |
| `MsgId` | `integer` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 76. 下载文件 (`/api/download_file`)

> **功能说明**：这些参数都来自于消息里

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/download_file`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "from_user": "",
  "total_len": "31538",
  "MsgId": 2754393265637994500,
  "path": "d:\\罗泽南8月考勤表.xlsx",
  "attachid": "@cdn_3057020100044b3049020100020403e0b2d502032df85f020426372f70020468baeb70042439666537653139352d646235392d343035662d613664372d3231393862356533326130350204011800050201000405004c57c300_f2db3329fe5ea2fd06e2ad245da1965e_1",
  "type": "6"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `from_user` | `string` |  (必填) |
| `total_len` | `string` |  (必填) |
| `MsgId` | `number` |  (必填) |
| `path` | `string` |  (必填) |
| `attachid` | `string` |  (必填) |
| `type` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 77. 下载语音 (`/api/download_voice`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/download_voice`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "newMsgId": "",
  "length": "",
  "MsgId": "",
  "path": ""
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `newMsgId` | `string` |  (必填) |
| `length` | `string` |  (必填) |
| `MsgId` | `string` |  (必填) |
| `path` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 78. 语音消息转文字 (`/api/get_voice_trans`)

> **功能说明**：对收到的语音消息触发服务器语音识别转换为文字

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_voice_trans`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "clientMsgId": "1000293120",
  "length": 1024
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `clientMsgId` | `string` | 语音消息的客户端唯一消息 ID |
| `length` | `number` | 语音数据长度 |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回语音识别后的文本结果字符串。

---
### 79. 获取配置文件保存目录 (`/api/get_config_path`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_config_path`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "code": 1,
    "configPath": "C:\\Users\\Admin\\AppData\\Roaming\\WechatTools\\config.ini",
    "data": null
}
```

---
## 七、微信支付与红包模块 (Pay & RedPackets)

> 包含生成支付二维码、转账自动确认/退还、接收红包、拆红包以及企业定制红包操作等接口。

### 80. 生成个人收款/支付二维码 (`/api/generate_payqrcode`)

> **功能说明**：生成指定金额或自定义格式的微信支付/收款二维码

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/generate_payqrcode`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "url": "weixin://wxpay/bizpayurl?...",
  "urlType": 1
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `url` | `string` | 支付或收款协议 URL |
| `urlType` | `number` | 二维码类型 |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回生成的二维码 base64 数据或解析后的二维码图片链接。

---
### 81. 确认收款 (`/api/ten_pay_trans_fer_confirm`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/ten_pay_trans_fer_confirm`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "invalid_time": 0,
  "transferid": "string"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `invalid_time` | `number` | 过期时间来自消息里的 invalid_time (必填) |
| `transferid` | `string` | 传输ID 来自消息里的 transferid (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 82. 拒绝收款 (`/api/un_ten_pay_trans_fer_confirm`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/un_ten_pay_trans_fer_confirm`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "invalid_time": 0,
  "transferid": "string"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `invalid_time` | `number` | 过期时间来自消息里的 invalid_time (必填) |
| `transferid` | `string` | 传输ID 来自消息里的 transferid (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 83. 接收/查看微信红包 (`/api/receivewxhb`)

> **功能说明**：查看微信红包详情，获取红包是否已被领取等状态

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/receivewxhb`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "channelId": "1",
  "msgType": "49",
  "nativeUrl": "wxpay://...",
  "sendId": "1000039201..."
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `channelId` | `string` | 红包渠道 ID |
| `msgType` | `string` | 红包消息类型数值（通常为 49） |
| `nativeUrl` | `string` | 红包 XML 中解析出的 nativeUrl 链接 |
| `sendId` | `string` | 红包发送方 ID / 业务编号 |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回红包基本信息及领取状态。

---
### 84. 打开微信红包 (拆红包) (`/api/openwxhb`)

> **功能说明**：执行拆红包操作，领取红包金额

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/openwxhb`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "channelId": "1",
  "msgType": "49",
  "nativeUrl": "wxpay://...",
  "sendId": "1000039201...",
  "from_wxid": "wxid_sender"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `channelId` | `string` | 红包渠道 ID |
| `msgType` | `string` | 消息类型（通常为 49） |
| `nativeUrl` | `string` | 红包 nativeUrl 链接 |
| `sendId` | `string` | 红包唯一标识 sendId |
| `from_wxid` | `string` | 发红包者的微信 ID |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回拆红包结果（金额、抢到红包用户列表等信息）。

---
### 85. 打开定制红包 (VIP通道) (`/api/openwxhb_vip`)

> **功能说明**：特定活动/企业定制红包拆取通道

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/openwxhb_vip`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "channelId": "1",
  "msgType": "49",
  "nativeUrl": "wxpay://...",
  "sendId": "1000039201..."
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `channelId` | `string` | 红包渠道 ID |
| `msgType` | `string` | 消息类型数值 |
| `nativeUrl` | `string` | 红包 nativeUrl 链接 |
| `sendId` | `string` | 红包 sendId |

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回拆红包结果。

---
## 八、系统与底层数据库模块 (System & Database)

> 包含微信初始化、接口健康检查、消息防撤回控制、获取A8Key、网页登录凭证、SQLite数据库句柄及执行、收藏列表等高级接口。

### 86. 微信初始化_删除当前设备_慎用 (`/api/wechat_init`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/wechat_init`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 87. 接口连通性测试 (`/api/test`)

> **功能说明**：测试微信 Hook HTTP 服务是否正常运行

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/test`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回成功状态码，例如 `{"errCode": 1, "errMsg": "ok"}`。

---
### 88. 防撤回 (`/api/anti_revoke`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/anti_revoke`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "swtich": "true"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `swtich` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 89. 获取A8key (`/api/get_a8key`)

> **功能说明**：a8key有很多场景 展示的是群聊邀请a8key 其他请自行获取

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_a8key`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "url": "https://support.weixin.qq.com/cgi-bin/mmsupport-bin/addchatroombyinvite?ticket=AwfZ4kSJ9P2FbmFK6LPrpg%3D%3D",
  "urlType": "0",
  "scene": "0"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `url` | `string` |  (必填) |
| `urlType` | `string` |  (必填) |
| `scene` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
### 90. 获取小程序code (`/api/js_login`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/js_login`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "waId": "wxaf48360fec8b1f0c"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `waId` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "result": "fail",
    "msg": "protobuf parse failed"
}
```

---
### 91. 自动登录微信 (`/api/auto_login`)

> **功能说明**：唤起并尝试使用本地已保存的 Session/凭据自动登录微信

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/auto_login`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
返回自动登录状态结果。

---
### 92. 获取数据库句柄 (`/api/get_db_handle`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_db_handle`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
    "data": [
        {
            "handle": 1984621919104,
            "name": "message_resource.db"
        },
        {
            "handle": 1984543430864,
            "name": "head_image.db"
        },
        {
            "handle": 1984543433120,
            "name": "biz_message_0.db"
        },
        {
            "handle": 1984543433872,
            "name": "key_info.db"
        },
        {
            "handle": 1984543427104,
            "name": "message_0.db"
        },
        {
            "handle": 1984543427856,
            "name": "contact.db"
        },
        {
            "handle": 1984543429360,
            "name": "message_fts.db"
        },
        {
            "handle": 1984543430112,
            "name": "message_1.db"
        },
        {
            "handle": 1984543431616,
            "name": "contact_fts.db"
        },
        {
            "handle": 1984543432368,
            "name": "session.db"
        },
        {
            "handle": 1984621918352,
            "name": "hardlink.db"
        },
        {
            "handle": 1984543428608,
            "name": "media_0.db"
        },
        {
            "handle": 1984621917600,
            "name": "emoticon.db"
        },
        {
            "handle": 1984621920608,
            "name": "favorite.db"
        },
        {
            "handle": 1984621916096,
            "name": "sns.db"
        },
        {
            "handle": 1984621916848,
            "name": "favorite_fts.db"
        },
        {
            "handle": 1984621919856,
            "name": "general.db"
        }
    ]
}
```

---
### 93. 执行数据库查询 (`/api/sqlite3_exec`)

> **功能说明**：查询前 先查询一下表结构 {"db_name":"contact.db","sql_fmt":"select * from sqlite_master"}
带二进制数据的话就忽略那个字段

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sqlite3_exec`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{
  "db_name": "contact.db",
  "sql_fmt": "SELECT username FROM contact WHERE username LIKE '%chatroom%'"
}
```

| 参数名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `db_name` | `string` |  (必填) |
| `sql_fmt` | `string` |  (必填) |

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
[
    {
        "draft": "",
        "is_hidden": "0",
        "last_clear_unread_timestamp": "1747833113",
        "last_msg_ext_type": "0",
        "last_msg_locald_id": "0",
        "last_msg_sender": "",
        "last_msg_sub_type": "0",
        "last_msg_type": "0",
        "last_sender_display_name": "",
        "last_timestamp": "0",
        "sort_timestamp": "0",
        "status": "0",
        "summary": "",
        "type": "0",
        "unread_count": "0",
        "unread_first_msg_srv_id": "0",
        "unread_first_pat_msg_local_id": "0",
        "unread_first_pat_msg_sort_seq": "0",
        "username": "gh_166a0f0b7ce3"
    }
]
```

---
### 94. 获取收藏列表 (`/api/get_fav_list`)


#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/get_fav_list`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回调用状态或数据结果。
```json
{
  "errCode": 1,
  "errMsg": "成功"
}
```

---
## 九、其他未分类接口

### 95. sns_get_user_page (`/api/sns_get_user_page`)

> **功能说明**：底层已实现的 Hook API 接口

#### 1. 接口地址与方法
- **URL**: `http://127.0.0.1:19088/api/sns_get_user_page`
- **Method**: `POST`
- **Content-Type**: `application/json`

#### 2. 请求参数 (JSON)
```json
{}
```

*无需请求体参数或参数为空对象。*

#### 3. 数据接收与解析机制
HTTP 接口响应：
立即返回 `{"errCode": 1}`。

---
