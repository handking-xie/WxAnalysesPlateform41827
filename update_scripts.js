const fs = require('fs');

let f1 = fs.readFileSync('export_docs.js', 'utf8');
f1 = f1.replace(/\/api\/sns_get_user_page/g, '/api/sns_get_user_page2');
f1 = f1.replace(/reqJson: \{\s*to_wxid:\s*'wxid_xxxxxxxxxxxxxx',\s*maxId:\s*'0'\s*\}/, "reqJson: { to_wxid: 'wxid_xxxxxxxxxxxxxx', firstPageMd5: '', maxId: '0' }");
f1 = f1.replace(/params: \[\s*\{\s*name:\s*'to_wxid'[^}]+},\s*\{\s*name:\s*'maxId'/, "params: [\n      { name: 'to_wxid', type: 'string', desc: '目标好友的微信 ID（如 wxid_xxx 或设置的微信号）' },\n      { name: 'firstPageMd5', type: 'string', desc: '第一页 MD5 校验串，首次拉取或不校验可传空字符串 \"\"' },\n      { name: 'maxId'");
fs.writeFileSync('export_docs.js', f1, 'utf8');

let f2 = fs.readFileSync('generate_apifox_import.js', 'utf8');
f2 = f2.replace(/\/api\/sns_get_user_page/g, '/api/sns_get_user_page2');
f2 = f2.replace(/reqJson: \{\s*to_wxid:\s*'wxid_xxxxxxxxxxxxxx',\s*maxId:\s*'0'\s*\}/, "reqJson: { to_wxid: 'wxid_xxxxxxxxxxxxxx', firstPageMd5: '', maxId: '0' }");
f2 = f2.replace(/params: \[\s*\{\s*name:\s*'to_wxid'[^}]+},\s*\{\s*name:\s*'maxId'/, "params: [\n      { name: 'to_wxid', type: 'string', desc: '目标好友的微信 ID（如 wxid_xxx 或设置的微信号）' },\n      { name: 'firstPageMd5', type: 'string', desc: '第一页 MD5 校验串，首次拉取或不校验可传空字符串 \"\"' },\n      { name: 'maxId'");
fs.writeFileSync('generate_apifox_import.js', f2, 'utf8');

console.log('Update scripts completed.');
