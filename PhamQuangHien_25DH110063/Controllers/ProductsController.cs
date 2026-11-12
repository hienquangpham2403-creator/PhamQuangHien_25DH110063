using System.Linq;
using System.Web.Mvc;
using PhamQuangHien_25DH110063.Models;

namespace PhamQuangHien_25DH110063.Controllers
{
    public class ProductsController : Controller
    {
        // Khai báo biến kết nối CSDL
        Warehouse_MSSVEntities db = new Warehouse_MSSVEntities();

        // --- HIỂN THỊ DANH SÁCH ---
        public ActionResult Index()
        {
            var products = db.Products.ToList();
            return View(products);
        }

        // --- CHỈNH SỬA SẢN PHẨM ---
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Index");
            }

            var sp = db.Products.Find(id);
            if (sp == null)
            {
                return HttpNotFound();
            }
            return View(sp);
        }

        [HttpPost]
        public ActionResult Edit(Products model)
        {
            if (string.IsNullOrWhiteSpace(model.ProductName) ||
                string.IsNullOrWhiteSpace(model.Category) ||
                model.Price <= 0)
            {
                ModelState.AddModelError("", "Lỗi: Tên và danh mục không được để trống, Giá phải lớn hơn 0!");
                return View(model);
            }

            var spInDb = db.Products.Find(model.ProductID);
            if (spInDb != null)
            {
                spInDb.ProductName = model.ProductName;
                spInDb.Category = model.Category;
                spInDb.Price = model.Price;
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // --- TRA CỨU HÀNG HÓA ---
        public ActionResult Search(string keyword, int? status, string sortOrder)
        {
            var query = db.Products.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(p => p.ProductName.ToLower().Contains(keyword.ToLower()));
            }

            if (status.HasValue)
            {
                if (status == 0)
                    query = query.Where(p => p.Quantity == 0);
                else if (status == 1)
                    query = query.Where(p => p.Quantity >= 1 && p.Quantity <= 4);
                else if (status == 5)
                    query = query.Where(p => p.Quantity >= 5);
            }

            if (sortOrder == "asc")
                query = query.OrderBy(p => p.Price);
            else if (sortOrder == "desc")
                query = query.OrderByDescending(p => p.Price);
            else
                query = query.OrderBy(p => p.ProductID);

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.SortOrder = sortOrder;

            return View(query.ToList());
        }

        // --- CẬP NHẬT KIỂM KÊ 1 SẢN PHẨM (CÂU 4) ---
        public ActionResult InventoryUpdate(int? id)
        {
            if (id == null) return RedirectToAction("Index");
            var sp = db.Products.Find(id);
            if (sp == null) return HttpNotFound();
            return View(sp);
        }

        [HttpPost]
        public ActionResult InventoryUpdate(int ProductID, int actualQuantity)
        {
            if (actualQuantity < 0)
            {
                TempData["Error"] = "Lỗi: Số lượng thực tế phải là số nguyên không âm (>= 0)!";
                return RedirectToAction("InventoryUpdate", new { id = ProductID });
            }

            var sp = db.Products.Find(ProductID);
            if (sp != null)
            {
                // Đã fix chuẩn xác phép tính chênh lệch có dấu trừ (-)
                int diff = actualQuantity - sp.Quantity;

                if (diff > 0)
                    TempData["Message"] = $"Chênh lệch tăng: {diff}";
                else if (diff < 0)
                    TempData["Message"] = $"Chênh lệch giảm: {System.Math.Abs(diff)}";
                else
                    TempData["Message"] = "Không chênh lệch";

                sp.Quantity = actualQuantity;
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // --- KIỂM KÊ ĐỒNG THỜI HAI SẢN PHẨM (CÂU 5) ---
        public ActionResult BatchInventoryUpdate()
        {
            ViewBag.ProductList = new SelectList(db.Products, "ProductID", "ProductName");
            return View();
        }

        [HttpPost]
        public ActionResult BatchInventoryUpdate(int id1, int qty1, int id2, int qty2)
        {
            ViewBag.ProductList = new SelectList(db.Products, "ProductID", "ProductName");

            if (id1 == id2 || qty1 < 0 || qty2 < 0)
            {
                TempData["Error"] = "Lỗi: Hai sản phẩm phải khác nhau và số lượng thực tế phải là số nguyên không âm!";
                return View();
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var p1 = db.Products.Find(id1);
                    var p2 = db.Products.Find(id2);

                    if (p1 == null || p2 == null)
                    {
                        transaction.Rollback();
                        TempData["Error"] = "Lỗi: Một trong hai sản phẩm không tồn tại trong CSDL.";
                        return View();
                    }

                    p1.Quantity = qty1;
                    p2.Quantity = qty2;
                    db.SaveChanges();

                    transaction.Commit();
                    TempData["Message"] = "Kiểm kê đồng thời hai sản phẩm thành công!";
                    return RedirectToAction("Index");
                }
                catch
                {
                    transaction.Rollback();
                    TempData["Error"] = "Giao dịch thất bại, toàn bộ thay đổi đã bị hủy bỏ.";
                    return View();
                }
            }
        }

        // --- THÊM SẢN PHẨM CÓ KIỂM TRA TRÙNG TÊN (CÂU 6) ---
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(Products model)
        {
            if (string.IsNullOrWhiteSpace(model.ProductName) ||
                string.IsNullOrWhiteSpace(model.Category) ||
                model.Price <= 0 ||
                model.Quantity < 0)
            {
                ModelState.AddModelError("", "Lỗi: Tên và danh mục không được để trống, Giá > 0 và Số lượng không được âm!");
                return View(model);
            }

            // Chuẩn hóa tên để kiểm tra trùng (bỏ khoảng trắng 2 đầu và chuyển chữ thường)
            string normalizedName = model.ProductName.Trim().ToLower();
            bool isExist = db.Products.Any(p => p.ProductName.Trim().ToLower() == normalizedName);

            if (isExist)
            {
                ModelState.AddModelError("ProductName", "Tên sản phẩm đã tồn tại trong kho!");
                return View(model);
            }

            db.Products.Add(model);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // --- THỐNG KÊ KẾT QUẢ KIỂM KÊ (CÂU 7) ---
        public ActionResult Statistics()
        {
            // Dùng LINQ và EF để tính toán trực tiếp từ CSDL
            ViewBag.TotalProducts = db.Products.Count();
            ViewBag.OutOfStock = db.Products.Count(p => p.Quantity == 0);
            ViewBag.TotalInventoryValue = db.Products.Sum(p => (decimal?)(p.Price * p.Quantity)) ?? 0;

            return View();
        }
    }
}