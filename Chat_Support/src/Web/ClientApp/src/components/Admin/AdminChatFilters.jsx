import React, { useState } from "react";
import { Row, Col, Form, Button } from "react-bootstrap";
import DatePicker from "react-multi-date-picker";
import DateObject from "react-date-object";
import persian from "react-date-object/calendars/persian";
import persian_fa from "react-date-object/locales/persian_fa";
import "react-multi-date-picker/styles/colors/teal.css";
import "./AdminChatFilters.css";

const AdminChatFilters = ({ filters, onApply, onClose }) => {
  const [localFilters, setLocalFilters] = useState(filters);

  const handleChange = (field, value) => {
    setLocalFilters((prev) => ({ ...prev, [field]: value }));
  };

  // Helper to convert Persian date to Gregorian Date object
  const handleDateChange = (field, dateObject) => {
    if (!dateObject) {
      handleChange(field, null);
      return;
    }
    // Convert Persian DateObject to JavaScript Date (Gregorian)
    const gregorianDate = dateObject.toDate();
    handleChange(field, gregorianDate);
  };

  // Convert JavaScript Date to Persian DateObject for display in DatePicker
  const convertToDateObject = (date) => {
    if (!date) return null;
    try {
      // Create a DateObject from JavaScript Date with Persian calendar
      return new DateObject(new Date(date)).convert(persian, persian_fa);
    } catch {
      return null;
    }
  };

  const handleApplyFilters = () => {
    onApply(localFilters);
  };

  const handleReset = () => {
    const resetFilters = {
      chatRoomType: null,
      regionId: null,
      createdFrom: null,
      createdTo: null,
      isDeleted: null,
      isGroup: null,
      minMembersCount: null,
      maxMembersCount: null,
      minMessagesCount: null,
      maxMessagesCount: null,
      lastActivityFrom: null,
      lastActivityTo: null,
      sortBy: "LastActivity",
      isDescending: true,
    };
    setLocalFilters(resetFilters);
    onApply(resetFilters);
  };

  return (
    <div className="advanced-filters mt-4 pt-3 border-top">
      <Row>
        {/* نوع چت */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">نوع چت</Form.Label>
            <Form.Select
              value={localFilters.chatRoomType ?? ""}
              onChange={(e) =>
                handleChange(
                  "chatRoomType",
                  e.target.value === "" ? null : parseInt(e.target.value)
                )
              }
              size="sm"
            >
              <option value="">همه</option>
              <option value="0">خصوصی</option>
              <option value="1">پشتیبانی</option>
              <option value="2">گروهی</option>
            </Form.Select>
          </Form.Group>
        </Col>

        {/* حذف فیلد دسته‌بندی (تکراری با نوع چت) */}

        {/* مرتب‌سازی */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">مرتب‌سازی بر اساس</Form.Label>
            <Form.Select
              value={localFilters.sortBy}
              onChange={(e) => handleChange("sortBy", e.target.value)}
              size="sm"
            >
              <option value="CreatedAt">تاریخ ایجاد</option>
              <option value="LastActivity">آخرین فعالیت</option>
              <option value="Name">نام چت</option>
              <option value="MembersCount">تعداد اعضا</option>
              <option value="MessagesCount">تعداد پیام‌ها</option>
            </Form.Select>
          </Form.Group>
        </Col>

        {/* ترتیب مرتب‌سازی */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">ترتیب</Form.Label>
            <Form.Select
              value={localFilters.isDescending}
              onChange={(e) =>
                handleChange("isDescending", e.target.value === "true")
              }
              size="sm"
            >
              <option value="true">نزولی (جدیدترین)</option>
              <option value="false">صعودی (قدیمی‌ترین)</option>
            </Form.Select>
          </Form.Group>
        </Col>

        {/* تاریخ ایجاد از */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">تاریخ ایجاد از</Form.Label>
            <DatePicker
              value={convertToDateObject(localFilters.createdFrom)}
              onChange={(date) => handleDateChange("createdFrom", date)}
              calendar={persian}
              locale={persian_fa}
              format="YYYY/MM/DD"
              placeholder="انتخاب تاریخ"
              inputClass="form-control form-control-sm"
              containerStyle={{ width: "100%" }}
              style={{ width: "100%" }}
            />
          </Form.Group>
        </Col>

        {/* تاریخ ایجاد تا */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">تاریخ ایجاد تا</Form.Label>
            <DatePicker
              value={convertToDateObject(localFilters.createdTo)}
              onChange={(date) => handleDateChange("createdTo", date)}
              calendar={persian}
              locale={persian_fa}
              format="YYYY/MM/DD"
              placeholder="انتخاب تاریخ"
              inputClass="form-control form-control-sm"
              containerStyle={{ width: "100%" }}
              style={{ width: "100%" }}
            />
          </Form.Group>
        </Col>

        {/* حداقل تعداد اعضا */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">حداقل اعضا</Form.Label>
            <Form.Control
              type="number"
              min="0"
              value={localFilters.minMembersCount ?? ""}
              onChange={(e) =>
                handleChange(
                  "minMembersCount",
                  e.target.value ? parseInt(e.target.value) : null
                )
              }
              size="sm"
            />
          </Form.Group>
        </Col>

        {/* حداکثر تعداد اعضا */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">حداکثر اعضا</Form.Label>
            <Form.Control
              type="number"
              min="0"
              value={localFilters.maxMembersCount ?? ""}
              onChange={(e) =>
                handleChange(
                  "maxMembersCount",
                  e.target.value ? parseInt(e.target.value) : null
                )
              }
              size="sm"
            />
          </Form.Group>
        </Col>

        {/* حداقل تعداد پیام‌ها */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">حداقل پیام‌ها</Form.Label>
            <Form.Control
              type="number"
              min="0"
              value={localFilters.minMessagesCount ?? ""}
              onChange={(e) =>
                handleChange(
                  "minMessagesCount",
                  e.target.value ? parseInt(e.target.value) : null
                )
              }
              size="sm"
            />
          </Form.Group>
        </Col>

        {/* حداکثر تعداد پیام‌ها */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">حداکثر پیام‌ها</Form.Label>
            <Form.Control
              type="number"
              min="0"
              value={localFilters.maxMessagesCount ?? ""}
              onChange={(e) =>
                handleChange(
                  "maxMessagesCount",
                  e.target.value ? parseInt(e.target.value) : null
                )
              }
              size="sm"
            />
          </Form.Group>
        </Col>

        {/* آخرین فعالیت از */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">آخرین فعالیت از</Form.Label>
            <DatePicker
              value={convertToDateObject(localFilters.lastActivityFrom)}
              onChange={(date) => handleDateChange("lastActivityFrom", date)}
              calendar={persian}
              locale={persian_fa}
              format="YYYY/MM/DD"
              placeholder="انتخاب تاریخ"
              inputClass="form-control form-control-sm"
              containerStyle={{ width: "100%" }}
              style={{ width: "100%" }}
            />
          </Form.Group>
        </Col>

        {/* آخرین فعالیت تا */}
        <Col md={6} lg={3} className="mb-3">
          <Form.Group>
            <Form.Label className="small text-muted">آخرین فعالیت تا</Form.Label>
            <DatePicker
              value={convertToDateObject(localFilters.lastActivityTo)}
              onChange={(date) => handleDateChange("lastActivityTo", date)}
              calendar={persian}
              locale={persian_fa}
              format="YYYY/MM/DD"
              placeholder="انتخاب تاریخ"
              inputClass="form-control form-control-sm"
              containerStyle={{ width: "100%" }}
              style={{ width: "100%" }}
            />
          </Form.Group>
        </Col>
      </Row>

      {/* دکمه‌های اعمال و بستن */}
      <Row className="mt-3">
        <Col className="d-flex justify-content-end gap-2">
          <Button variant="secondary" size="sm" onClick={onClose}>
            بستن
          </Button>
          <Button variant="outline-danger" size="sm" onClick={handleReset}>
            ریست فیلترها
          </Button>
          <Button variant="primary" size="sm" onClick={handleApplyFilters}>
            اعمال فیلترها
          </Button>
        </Col>
      </Row>
    </div>
  );
};

export default AdminChatFilters;
