

import java.net.InetSocketAddress;
import java.net.ServerSocket;

public class MySocketDemo {
    private ServerSocket serverSocket;
    private final String host = "0.0.0.0";
    private final int port = 61108;
    private volatile boolean isRunning = true;

    public void run() {
        try {
            System.out.println("初始化Socket Service,address:" + host + ",port:" + port);
            // 初始化服务器Socket
            serverSocket = new ServerSocket();
            serverSocket.bind(new InetSocketAddress(host, port));
            System.out.println("MySocketService 启动成功，ip：" + host + "，port：" + port);
            isRunning = true;
            acceptClientConnections();
        } catch (Exception e) {
            e.printStackTrace();
            System.out.println("Server error: " + e.getMessage());
        }
    }

    /**
     * 线程循环接受客户端连接
     */
    public void acceptClientConnections() {
        // 循环接受客户端连接（单线程，如需多线程可修改）
        try {
            new Thread(() -> {
                while (isRunning) {
                    try {
                        var clientSocket = serverSocket.accept();
                        System.out.println("接受到客户端连接:" + clientSocket.getInetAddress().getHostAddress() + ":" + clientSocket.getPort());
                        new MySocketDemoHandler(clientSocket).start();
                    } catch (Exception e) {
                        if (isRunning) { // 非主动关闭时打印异常
                            e.printStackTrace();
                            System.out.println("接受客户端连接异常:" + e);
                        }
                    }
                }
            }).start();
        } catch (Exception e) {
            e.printStackTrace();
            System.out.println("接受客户端连接异常:" + e);
        }
    }

    /**
     * 优雅关闭服务（Spring 容器销毁时执行）
     */
    public void stop() {
        System.out.println("=== 开始关闭 Socket 服务 ===");
        isRunning = false;
        try {
            if (serverSocket != null && !serverSocket.isClosed()) {
                serverSocket.close();
                System.out.println("Socket 服务已关闭");
            }
        } catch (Exception e) {
            System.out.println("关闭 Socket 服务失败:" + e);
        }
    }
}