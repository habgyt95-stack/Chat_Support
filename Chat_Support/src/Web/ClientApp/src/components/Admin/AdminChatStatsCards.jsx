import React from "react";
import { Row, Col, Card } from "react-bootstrap";
import {
  ChatDots,
  People,
  Envelope,
  Activity,
  GraphUp,
  Calendar,
} from "react-bootstrap-icons";
import "./AdminChatStatsCards.css";

const AdminChatStatsCards = ({ stats }) => {
  const statsCards = [
    {
      title: "کل چت‌ها",
      value: stats.totalChats,
      icon: ChatDots,
      color: "primary",
      subtitle: `${stats.activeChats} فعال`,
    },
    {
      title: "کل پیام‌ها",
      value: stats.totalMessages.toLocaleString("fa-IR"),
      icon: Envelope,
      color: "success",
      subtitle: `${stats.todayMessages} امروز`,
    },
    {
      title: "کاربران",
      value: stats.totalUsers,
      icon: People,
      color: "info",
      subtitle: "کل کاربران سیستم",
    },
    {
      title: "میانگین پیام",
      value: stats.averageMessagesPerChat.toFixed(1),
      icon: GraphUp,
      color: "warning",
      subtitle: "به ازای هر چت",
    },
    {
      title: "چت‌های گروهی",
      value: stats.groupChats,
      icon: People,
      color: "success",
      subtitle: `میانگین ${stats.averageMembersPerGroup.toFixed(1)} عضو`,
    },
    {
      title: "چت‌های امروز",
      value: stats.todayNewChats,
      icon: Calendar,
      color: "danger",
      subtitle: "چت جدید",
    },
  ];

  return (
    <Row className="mb-4 admin-stats-cards">
      {statsCards.map((stat, index) => (
        <Col key={index} xs={12} sm={6} md={4} lg={2} className="mb-3">
          <Card className={`stat-card border-${stat.color}`}>
            <Card.Body className="p-0">
              <div className="d-flex justify-content-between align-items-start mb-2">
                <div>
                  <div className="stat-title text-muted small">{stat.title}</div>
                  <div className={`stat-value text-${stat.color} fw-bold fs-4`}>
                    {stat.value}
                  </div>
                  <div className="stat-subtitle text-muted" style={{ fontSize: "0.75rem" }}>
                    {stat.subtitle}
                  </div>
                </div>
                <div className={`stat-icon bg-${stat.color} bg-opacity-10 text-${stat.color} rounded p-2`}>
                  <stat.icon size={24} />
                </div>
              </div>
            </Card.Body>
          </Card>
        </Col>
      ))}
    </Row>
  );
};

export default AdminChatStatsCards;
