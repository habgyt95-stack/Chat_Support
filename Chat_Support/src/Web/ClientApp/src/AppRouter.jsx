// src/AppRouter.jsx

import { Routes, Route, Navigate } from "react-router-dom";
import LiveChatWidget from "./components/Chat/LiveChatWidget.jsx";
import Chat from "./components/Chat/Chat.jsx";
import AgentDashboard from "./components/Chat/AgentDashboard.jsx";
import AgentManagement from "./components/Chat/AgentManagement.jsx";
import AdminChatDashboard from "./components/Admin/AdminChatDashboard.jsx";
import { ChatProvider } from "./contexts/ChatContext.jsx";
import ProtectedRoute from "./components/ProtectedRoute.jsx";
import { useAuth } from "./hooks/useAuth";
import Login from "./components/contexts/Login.jsx";

const Home = () => <h2>صفحه اصلی (عمومی)</h2>;

const AppRouter = () => {
  // Keep auth provider active; no direct use here to avoid unused var warnings
  useAuth();

  return (
    <div>
      {/* <nav>
        <Link to="/">خانه</Link> | <Link to="/chat">چت</Link>
        {isAuthenticated && user && (
          <span style={{ marginLeft: '20px' }}>
            خوش آمدید, {user.firstName}!
            <button onClick={logout} style={{ marginLeft: '10px' }}>خروج از چت</button>
          </span>
        )}
      </nav>
      <hr /> */}
      <Routes>
  <Route path="/" element={<Home />} />
        <Route
          path="/login"
          element={<Login />}
        />
        <Route
          path="/chat"
          element={
            <ProtectedRoute>
              <ChatProvider>
                <Chat />
              </ChatProvider>
            </ProtectedRoute>
          }
        />
        <Route
          path="/chat/:roomId"
          element={
            <ProtectedRoute>
              <ChatProvider>
                <Chat />
              </ChatProvider>
            </ProtectedRoute>
          }
        />
        <Route
          path="/AgentDashboard"
          element={
            <ProtectedRoute>
              <AgentDashboard />
            </ProtectedRoute>
          }
        />
        <Route
          path="/AgentManagement"
          element={
            <ProtectedRoute>
              <AgentManagement />
            </ProtectedRoute>
          }
        />
        <Route
          path="/admin/chats"
          element={
            <ProtectedRoute>
              <AdminChatDashboard />
            </ProtectedRoute>
          }
        />
        {/* سایر صفحات را اینجا اضافه کنید */}
      </Routes>
    </div>
  );
};

export default AppRouter;
