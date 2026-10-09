
import java.io.InputStream;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

public class MySocketDemoHandler extends Thread {
    private final Socket socket;
    private int receiveTime = 10000; // 10秒超时

    public MySocketDemoHandler(Socket socket) {
        super("MySocketHandler");
        this.socket = socket;
    }

    private void receiveMessage(String data) {
        System.out.println("收到消息：");
        System.out.println(data);
    }

    @Override
    public void run() {
        handleClient();
    }

    public void handleClient() {
        String clientAddress = socket.getInetAddress().getHostAddress() + ":" + socket.getPort();
        long uuid = System.currentTimeMillis();
        System.out.println("已将：" + clientAddress + "加入到线程，会话id：" + uuid + "，开始会话");
        String ip = socket.getInetAddress().getHostAddress();
        int port = socket.getPort();
        System.out.println("[" + ip + ":" + port + "]已经连接到服务，开始接收数据");

        try (InputStream inputStream = socket.getInputStream()) { // 自动关闭流
            while (!Thread.currentThread().isInterrupted()) {
                // 1. 读取4字节长度头
                byte[] header = new byte[4];
                int headerRead = inputStream.read(header);
                if (headerRead != 4) {
                    throw new Exception("消息头不完整（仅读取" + headerRead + "字节），关闭连接");
                }

                // 2. 解析长度（先尝试大端序，失败则尝试小端序）
                int length;
                try {
                    // 大端序（network byte order）
                    length = ((header[0] & 0xFF) << 24) |
                            ((header[1] & 0xFF) << 16) |
                            ((header[2] & 0xFF) << 8) |
                            (header[3] & 0xFF);
                } catch (Exception e) {
                    // 小端序（备用方案）
                    length = ((header[3] & 0xFF) << 24) |
                            ((header[2] & 0xFF) << 16) |
                            ((header[1] & 0xFF) << 8) |
                            (header[0] & 0xFF);
                }

                // 校验长度合法性
                if (length <= 0 || length > 1024 * 1024) { // 限制最大1MB，防止恶意数据
                    throw new Exception("非法的消息长度：" + length);
                }

                // 3. 读取消息体（增加超时逻辑）
                byte[] messageBytes = new byte[length];
                int received = 0;
                long startTime = System.currentTimeMillis();

                while (received < length) {
                    // 检查超时
                    long elapsedTime = System.currentTimeMillis() - startTime;
                    if (elapsedTime > receiveTime) {
                        System.out.println("Read timeout (" + receiveTime / 1000 + "s) from " + clientAddress + ", discard current message");
                        // 清空缓冲区剩余数据
                        while (inputStream.available() > 0) {
                            inputStream.read();
                        }
                        throw new Exception("读取超时，关闭连接");
                    }

                    // 非阻塞检查是否有数据
                    if (inputStream.available() > 0) {
                        int read = inputStream.read(messageBytes, received, length - received);
                        if (read == -1) {
                            throw new RuntimeException("Connection closed prematurely by " + clientAddress);
                        }
                        received += read;
                        System.out.println("Received " + received + "/" + length + " bytes from " + clientAddress);
                    } else {
                        // 短暂休眠避免CPU空转
                        Thread.sleep(10);
                    }
                }

                // 4. 解码消息
                String message = new String(messageBytes, StandardCharsets.UTF_8);
                System.out.println("Received message (length: " + length + "): ");
                System.out.println(message);
                receiveMessage(message);
            }
        } catch (Exception e) {
            System.out.println("Error handling client " + clientAddress + ": " + e.getMessage());
        } finally {
            try {
                if (!socket.isClosed()) {
                    socket.close();
                    System.out.println("Connection closed with " + clientAddress);
                }
            } catch (Exception e) {
                System.out.println("Error closing client socket: " + e.getMessage());
            }
        }
    }
}