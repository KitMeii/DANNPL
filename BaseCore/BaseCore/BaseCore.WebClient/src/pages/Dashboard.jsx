import React, { useState, useEffect, useCallback } from "react";
import { productApi, userApi, categoryApi, orderApi } from "../services/api";
import { useAuth } from "../contexts/AuthContext";

const Dashboard = () => {
    const [stats, setStats] = useState({
        products: 0,
        categories: 0,
        users: 0,
        orders: 0,
    });
    const [loading, setLoading] = useState(true);
    const { user, isAdmin } = useAuth(); // Lấy user và hàm kiểm tra quyền từ Context

    const loadStats = useCallback(async () => {
        setLoading(true);
        try {
            // 1. Nhóm API mà ai cũng có quyền xem (Sản phẩm & Danh mục)
            const [productsRes, categoriesRes] = await Promise.all([
                productApi.getAll({ pageSize: 1 }),
                categoryApi.getAll(),
            ]);

            let usersCount = 0;
            let ordersCount = 0;

            // 2. Phân luồng gọi API theo vai trò để tránh lỗi 403 (Forbidden)
            if (isAdmin()) {
                // VAI TRÒ ADMIN: Lấy toàn bộ dữ liệu hệ thống
                const [usersRes, ordersRes] = await Promise.all([
                    userApi.getAll({ page: 1, pageSize: 1 }),
                    orderApi.getAll(),
                ]);
                usersCount = usersRes.data?.totalCount || 0;
                ordersCount = ordersRes.data?.totalCount || ordersRes.data?.length || 0;
            } else {
                // VAI TRÒ KHÁCH HÀNG: Chỉ lấy đơn hàng của chính mình
                try {
                    const myOrdersRes = await orderApi.getMyOrders();
                    ordersCount = myOrdersRes.data?.totalCount || myOrdersRes.data?.length || 0;
                } catch (err) {
                    console.warn("Không thể lấy số lượng đơn hàng cá nhân");
                }
            }

            setStats({
                products: productsRes.data?.totalCount || productsRes.data?.length || 0,
                categories: categoriesRes.data?.length || 0,
                users: usersCount,
                orders: ordersCount,
            });
        } catch (error) {
            console.error("Lỗi khi tải dữ liệu Dashboard:", error);
        } finally {
            setLoading(false);
        }
    }, [isAdmin]);

    useEffect(() => {
        loadStats();
    }, [loadStats]);

    return (
        <div className="content-wrapper">
            {/* Header */}
            <div className="content-header">
                <div className="container-fluid">
                    <div className="row mb-2">
                        <div className="col-sm-6">
                            <h1 className="m-0">Bảng điều khiển</h1>
                        </div>
                    </div>
                </div>
            </div>

            {/* Main content */}
            <section className="content">
                <div className="container-fluid">
                    {loading ? (
                        <div className="text-center py-5">
                            <div className="spinner-border text-primary" role="status">
                                <span className="sr-only">Đang tải...</span>
                            </div>
                        </div>
                    ) : (
                        <div className="row">
                            {/* Box Sản phẩm */}
                            <div className="col-lg-3 col-6">
                                <div className="small-box bg-info">
                                    <div className="inner">
                                        <h3>{stats.products}</h3>
                                        <p>Sản phẩm</p>
                                    </div>
                                    <div className="icon"><i className="fas fa-box"></i></div>
                                    <a href="/products" className="small-box-footer">Chi tiết <i className="fas fa-arrow-circle-right"></i></a>
                                </div>
                            </div>

                            {/* Box Danh mục */}
                            <div className="col-lg-3 col-6">
                                <div className="small-box bg-success">
                                    <div className="inner">
                                        <h3>{stats.categories}</h3>
                                        <p>Danh mục</p>
                                    </div>
                                    <div className="icon"><i className="fas fa-tags"></i></div>
                                    <a href="/categories" className="small-box-footer">Chi tiết <i className="fas fa-arrow-circle-right"></i></a>
                                </div>
                            </div>

                            {/* Box Đơn hàng - Tự động đổi nhãn theo vai trò */}
                            <div className="col-lg-3 col-6">
                                <div className="small-box bg-danger">
                                    <div className="inner">
                                        <h3>{stats.orders}</h3>
                                        <p>{isAdmin() ? "Tổng đơn hàng" : "Đơn hàng của tôi"}</p>
                                    </div>
                                    <div className="icon"><i className="fas fa-shopping-cart"></i></div>
                                    <a href="/orders" className="small-box-footer">Xem đơn <i className="fas fa-arrow-circle-right"></i></a>
                                </div>
                            </div>

                            {/* Box Users - CHỈ HIỆN CHO ADMIN */}
                            {isAdmin() && (
                                <div className="col-lg-3 col-6">
                                    <div className="small-box bg-warning">
                                        <div className="inner">
                                            <h3>{stats.users}</h3>
                                            <p>Người dùng</p>
                                        </div>
                                        <div className="icon"><i className="fas fa-users"></i></div>
                                        <a href="/users" className="small-box-footer">Quản lý <i className="fas fa-arrow-circle-right"></i></a>
                                    </div>
                                </div>
                            )}
                        </div>
                    )}

                    {/* Welcome Card */}
                    <div className="row">
                        <div className="col-12">
                            <div className="card shadow-sm">
                                <div className="card-header bg-white">
                                    <h3 className="card-title text-bold">
                                        Chào mừng {user?.fullName || user?.username || 'bạn'} quay trở lại!
                                    </h3>
                                </div>
                                <div className="card-body">
                                    <p className="text-muted">
                                        Hệ thống quản lý <strong>Revo Coffee</strong> phiên bản dành cho {isAdmin() ? "Quản trị viên" : "Khách hàng"}.
                                    </p>
                                    <div className="alert alert-light border">
                                        <h5><i className="icon fas fa-info"></i> Ghi chú hệ thống:</h5>
                                        {isAdmin() ?
                                            "Bạn có toàn quyền quản lý kho hàng, người dùng và xử lý mọi đơn hàng trên hệ thống." :
                                            "Bạn có thể xem các sản phẩm mới nhất và theo dõi lộ trình đơn hàng mình đã đặt."
                                        }
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </section>
        </div>
    );
};

export default Dashboard;