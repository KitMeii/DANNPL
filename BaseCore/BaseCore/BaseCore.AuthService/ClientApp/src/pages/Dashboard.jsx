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
    const { user, isAdmin } = useAuth(); // L?y user và hàm ki?m tra quy?n t? Context

    const loadStats = useCallback(async () => {
        setLoading(true);
        try {
            // 1. Nhóm API mà ai c?ng có quy?n xem (S?n ph?m & Danh m?c)
            const [productsRes, categoriesRes] = await Promise.all([
                productApi.getAll({ pageSize: 1 }),
                categoryApi.getAll(),
            ]);

            let usersCount = 0;
            let ordersCount = 0;

            // 2. Phân lu?ng g?i API theo vai trò ?? tránh l?i 403 (Forbidden)
            if (isAdmin()) {
                // VAI TRÒ ADMIN: L?y toàn b? d? li?u h? th?ng
                const [usersRes, ordersRes] = await Promise.all([
                    userApi.getAll({ page: 1, pageSize: 1 }),
                    orderApi.getAll(),
                ]);
                usersCount = usersRes.data?.totalCount || 0;
                ordersCount = ordersRes.data?.totalCount || ordersRes.data?.length || 0;
            } else {
                // VAI TRÒ KHÁCH HÀNG: Ch? l?y ??n hàng c?a chính mình
                try {
                    const myOrdersRes = await orderApi.getMyOrders();
                    ordersCount = myOrdersRes.data?.totalCount || myOrdersRes.data?.length || 0;
                } catch (err) {
                    console.warn("Không th? l?y s? l??ng ??n hàng cá nhân");
                }
            }

            setStats({
                products: productsRes.data?.totalCount || productsRes.data?.length || 0,
                categories: categoriesRes.data?.length || 0,
                users: usersCount,
                orders: ordersCount,
            });
        } catch (error) {
            console.error("L?i khi t?i d? li?u Dashboard:", error);
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
                            <h1 className="m-0">B?ng ?i?u khi?n</h1>
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
                                <span className="sr-only">?ang t?i...</span>
                            </div>
                        </div>
                    ) : (
                        <div className="row">
                            {/* Box S?n ph?m */}
                            <div className="col-lg-3 col-6">
                                <div className="small-box bg-info">
                                    <div className="inner">
                                        <h3>{stats.products}</h3>
                                        <p>S?n ph?m</p>
                                    </div>
                                    <div className="icon"><i className="fas fa-box"></i></div>
                                    <a href="/products" className="small-box-footer">Chi ti?t <i className="fas fa-arrow-circle-right"></i></a>
                                </div>
                            </div>

                            {/* Box Danh m?c */}
                            <div className="col-lg-3 col-6">
                                <div className="small-box bg-success">
                                    <div className="inner">
                                        <h3>{stats.categories}</h3>
                                        <p>Danh m?c</p>
                                    </div>
                                    <div className="icon"><i className="fas fa-tags"></i></div>
                                    <a href="/categories" className="small-box-footer">Chi ti?t <i className="fas fa-arrow-circle-right"></i></a>
                                </div>
                            </div>

                            {/* Box ??n hàng - T? ??ng ??i nhãn theo vai trò */}
                            <div className="col-lg-3 col-6">
                                <div className="small-box bg-danger">
                                    <div className="inner">
                                        <h3>{stats.orders}</h3>
                                        <p>{isAdmin() ? "T?ng ??n hàng" : "??n hàng c?a tôi"}</p>
                                    </div>
                                    <div className="icon"><i className="fas fa-shopping-cart"></i></div>
                                    <a href="/orders" className="small-box-footer">Xem ??n <i className="fas fa-arrow-circle-right"></i></a>
                                </div>
                            </div>

                            {/* Box Users - CH? HI?N CHO ADMIN */}
                            {isAdmin() && (
                                <div className="col-lg-3 col-6">
                                    <div className="small-box bg-warning">
                                        <div className="inner">
                                            <h3>{stats.users}</h3>
                                            <p>Ng??i dùng</p>
                                        </div>
                                        <div className="icon"><i className="fas fa-users"></i></div>
                                        <a href="/users" className="small-box-footer">Qu?n lý <i className="fas fa-arrow-circle-right"></i></a>
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
                                        Chào m?ng {user?.fullName || user?.username || 'b?n'} quay tr? l?i!
                                    </h3>
                                </div>
                                <div className="card-body">
                                    <p className="text-muted">
                                        H? th?ng qu?n lý <strong>Revo Coffee</strong> phiên b?n dành cho {isAdmin() ? "Qu?n tr? viên" : "Khách hàng"}.
                                    </p>
                                    <div className="alert alert-light border">
                                        <h5><i className="icon fas fa-info"></i> Ghi chú h? th?ng:</h5>
                                        {isAdmin() ?
                                            "B?n có toàn quy?n qu?n lý kho hàng, ng??i dùng và x? lý m?i ??n hàng trên h? th?ng." :
                                            "B?n có th? xem các s?n ph?m m?i nh?t và theo dõi l? trình ??n hàng mình ?ã ??t."
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