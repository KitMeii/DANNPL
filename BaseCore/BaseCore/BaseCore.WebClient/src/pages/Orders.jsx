import React, { useState, useEffect, useCallback } from 'react';
import { orderApi } from '../services/api';
import { useAuth } from '../contexts/AuthContext';

const Orders = () => {
    const [orders, setOrders] = useState([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');
    const [page, setPage] = useState(1);
    const [pageSize] = useState(10);
    const [totalPages, setTotalPages] = useState(1);
    const { user, isAdmin } = useAuth();
    const [showModal, setShowModal] = useState(false);
    const [selectedOrder, setSelectedOrder] = useState(null);
    const [statusUpdate, setStatusUpdate] = useState('');

    const fetchOrders = useCallback(async () => {
        setLoading(true);
        try {
            const apiCall = isAdmin() ? orderApi.getAll : orderApi.getMyOrders;
            const response = await apiCall({ keyword: search, page, pageSize });

            const data = response.data.items || response.data || [];
            setOrders(data);

            const totalCount = response.data.totalCount || data.length || 0;
            setTotalPages(Math.ceil(totalCount / pageSize));
        } catch (error) {
            console.error("Lỗi khi lấy danh sách đơn hàng:", error);
        }
        setLoading(false);
    }, [isAdmin, search, page, pageSize]);

    useEffect(() => { fetchOrders(); }, [fetchOrders]);

    const handleSearch = (e) => { e.preventDefault(); setPage(1); fetchOrders(); };

    const handleDelete = async (id) => {
        if (window.confirm('Bạn có chắc muốn XÓA VĨNH VIỄN đơn hàng này?')) {
            try {
                await orderApi.delete(id);
                fetchOrders();
            } catch (error) { alert('Xóa thất bại!'); }
        }
    };

    const handleCancelOrder = async (id) => {
        if (window.confirm('Bạn có chắc muốn HỦY đơn hàng này không?')) {
            try {
                await orderApi.update(id, { status: 'Cancelled' });
                alert("Đã hủy đơn hàng thành công!");
                fetchOrders();
            } catch (error) { alert("Hủy đơn thất bại!"); }
        }
    };

    const handleViewDetail = async (id) => {
        try {
            const response = await orderApi.getById(id);
            const orderData = {
                ...(response.data.order || response.data),
                items: response.data.details || response.data.items || []
            };
            setSelectedOrder(orderData);
            setStatusUpdate(orderData.status);
            setShowModal(true);
        } catch (error) {
            alert("Không lấy được chi tiết đơn hàng");
        }
    };

    const handleUpdateStatus = async () => {
        try {
            await orderApi.update(selectedOrder.id, { ...selectedOrder, status: statusUpdate });
            alert("Cập nhật trạng thái thành công!");
            setShowModal(false);
            fetchOrders();
        } catch (error) { alert("Cập nhật thất bại"); }
    };

    return (
        <div className="content-wrapper">
            {/* Header trang giúp căn lề và tiêu đề đẹp hơn */}
            <div className="content-header">
                <div className="container-fluid">
                    <div className="row mb-2">
                        <div className="col-sm-6">
                            <h1 className="m-0">Quản lý đơn hàng</h1>
                        </div>
                    </div>
                </div>
            </div>

            {/* Nội dung chính */}
            <section className="content">
                <div className="container-fluid">
                    <div className="card shadow-sm">
                        <div className="card-header">
                            <h3 className="card-title">
                                {isAdmin() ? 'Danh sách đơn hàng toàn hệ thống' : 'Lịch sử mua hàng của tôi'}
                            </h3>
                            <div className="card-tools">
                                <form onSubmit={handleSearch} className="input-group input-group-sm" style={{ width: '250px' }}>
                                    <input type="text" className="form-control" placeholder="Tìm mã đơn..." value={search} onChange={(e) => setSearch(e.target.value)} />
                                    <div className="input-group-append">
                                        <button type="submit" className="btn btn-default"><i className="fas fa-search"></i></button>
                                    </div>
                                </form>
                            </div>
                        </div>

                        <div className="card-body table-responsive p-0">
                            {loading ? <div className="text-center p-4">Đang tải dữ liệu...</div> : (
                                <table className="table table-hover text-nowrap">
                                    <thead>
                                        <tr>
                                            <th>Mã đơn</th>
                                            <th>Ngày đặt</th>
                                            {isAdmin() && <th>Khách hàng (ID)</th>}
                                            <th>Tổng tiền</th>
                                            <th>Trạng thái</th>
                                            <th className="text-center">Thao tác</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {orders.length > 0 ? orders.map((order) => (
                                            <tr key={order.id}>
                                                <td><strong>ORD-{order.id}</strong></td>
                                                <td>{new Date(order.orderDate).toLocaleDateString('vi-VN')}</td>
                                                {isAdmin() && <td>{order.userId}</td>}
                                                <td className="text-primary font-weight-bold">{order.totalAmount?.toLocaleString()} VNĐ</td>
                                                <td>
                                                    <span className={`badge ${order.status === 'Completed' ? 'bg-success' :
                                                            order.status === 'Cancelled' ? 'bg-danger' : 'bg-warning'
                                                        }`}>
                                                        {order.status || 'Pending'}
                                                    </span>
                                                </td>
                                                <td className="text-center">
                                                    <button className="btn btn-sm btn-info mr-1" onClick={() => handleViewDetail(order.id)} title="Xem chi tiết">
                                                        <i className="fas fa-eye"></i>
                                                    </button>

                                                    {isAdmin() ? (
                                                        <button className="btn btn-sm btn-danger" onClick={() => handleDelete(order.id)} title="Xóa vĩnh viễn">
                                                            <i className="fas fa-trash"></i>
                                                        </button>
                                                    ) : (
                                                        order.status === 'Pending' && (
                                                            <button className="btn btn-sm btn-outline-danger" onClick={() => handleCancelOrder(order.id)} title="Hủy đơn hàng">
                                                                Hủy đơn
                                                            </button>
                                                        )
                                                    )}
                                                </td>
                                            </tr>
                                        )) : (
                                            <tr><td colSpan={6} className="text-center p-3">Không tìm thấy đơn hàng nào.</td></tr>
                                        )}
                                    </tbody>
                                </table>
                            )}
                        </div>

                        <div className="card-footer clearfix">
                            <ul className="pagination pagination-sm m-0 float-right">
                                <li className={`page-item ${page === 1 ? 'disabled' : ''}`}>
                                    <button className="page-link" onClick={() => setPage(p => p - 1)}>«</button>
                                </li>
                                {[...Array(totalPages)].map((_, i) => (
                                    <li key={i} className={`page-item ${page === i + 1 ? 'active' : ''}`}>
                                        <button className="page-link" onClick={() => setPage(i + 1)}>{i + 1}</button>
                                    </li>
                                ))}
                                <li className={`page-item ${page === totalPages ? 'disabled' : ''}`}>
                                    <button className="page-link" onClick={() => setPage(p => p + 1)}>»</button>
                                </li>
                            </ul>
                        </div>
                    </div>
                </div>

                {/* Modal Chi tiết đơn hàng */}
                {showModal && selectedOrder && (
                    <div className="modal fade show" style={{ display: 'block', backgroundColor: 'rgba(0,0,0,0.5)' }}>
                        <div className="modal-dialog modal-lg">
                            <div className="modal-content">
                                <div className="modal-header">
                                    <h4 className="modal-title">Chi tiết đơn hàng ORD-{selectedOrder.id}</h4>
                                    <button type="button" className="close" onClick={() => setShowModal(false)}><span>&times;</span></button>
                                </div>
                                <div className="modal-body">
                                    <div className="row mb-3">
                                        <div className="col-md-6">
                                            <p><strong>Ngày đặt:</strong> {new Date(selectedOrder.orderDate).toLocaleString('vi-VN')}</p>
                                            <p><strong>Tổng cộng:</strong> <span className="text-danger font-weight-bold">{selectedOrder.totalAmount?.toLocaleString()} VNĐ</span></p>
                                        </div>
                                        <div className="col-md-6 border-left">
                                            <label>Trạng thái hiện tại:</label>
                                            {isAdmin() ? (
                                                <select className="form-control" value={statusUpdate} onChange={(e) => setStatusUpdate(e.target.value)}>
                                                    <option value="Pending">Pending</option>
                                                    <option value="Processing">Processing</option>
                                                    <option value="Completed">Completed</option>
                                                    <option value="Cancelled">Cancelled</option>
                                                </select>
                                            ) : (
                                                <div className="mt-1">
                                                    <span className={`badge ${selectedOrder.status === 'Completed' ? 'bg-success' : 'bg-warning'}`}>
                                                        {selectedOrder.status}
                                                    </span>
                                                </div>
                                            )}
                                        </div>
                                    </div>

                                    <h5>Sản phẩm đã chọn:</h5>
                                    <table className="table table-sm table-bordered">
                                        <thead className="bg-light text-center">
                                            <tr><th>Sản phẩm</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th></tr>
                                        </thead>
                                        <tbody>
                                            {selectedOrder.items?.map((item, index) => (
                                                <tr key={index}>
                                                    <td>{item.productName}</td>
                                                    <td className="text-center">{item.quantity}</td>
                                                    <td className="text-right">{item.unitPrice.toLocaleString()}</td>
                                                    <td className="text-right font-weight-bold">{(item.quantity * item.unitPrice).toLocaleString()}</td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                                <div className="modal-footer">
                                    <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Đóng</button>
                                    {isAdmin() && (
                                        <button className="btn btn-primary" onClick={handleUpdateStatus}>Lưu thay đổi</button>
                                    )}
                                </div>
                            </div>
                        </div>
                    </div>
                )}
            </section>
        </div>
    );
};

export default Orders;