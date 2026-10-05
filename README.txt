MEP SHEET STUDIO — REVIT 2023

BẮT ĐẦU
- Build: .\build.ps1
- Build và test Excel ở nền: .\test.ps1
- Mở project trong Revit > Add-in Manager (Manual Mode) > Load:
  bin\Release\Revit2023\MEP_Sheet_Manager_Modeless.dll
- Chạy class: MEP_Sheet_Manager.GetAllSheetNamesCommand.
- Chọn class nằm dưới DLL Modeless, không chọn RevitSheetTests.

CẤU TRÚC PROJECT
MEP_Sheet_Manager.csproj           Project chính, mở bằng Visual Studio
build.ps1                         Build DLL Revit
 test.ps1                         Build và chạy test Excel (không điều khiển máy)
 src/
   Commands/                      Điểm vào từ Add-in Manager
     GetAllSheetNamesCommand.cs
   Models/                        Dữ liệu sheet dùng chung
     SheetInfo.cs
   Services/
     Revit/SheetService.cs         Đọc, kiểm tra và tạo sheet trong Revit
     Excel/SheetWorkbook.cs        Đọc/ghi workbook .xlsx
   Infrastructure/
     SheetExternalEventHandler.cs  Gửi yêu cầu vào ngữ cảnh Revit API
   UI/Wpf/
     SheetManagerWindow.xaml       Style và bố cục giao diện
     SheetManagerWindow.xaml.cs    Sự kiện UI và điều phối ExternalEvent
 tests/
   Workbook/WorkbookTests.cs       Test Excel chạy ở nền
   Revit/RevitSheetTests.cs        Test tạo sheet, phải chạy trong Revit
   WorkbookTests.csproj            Project test Excel
   RevitSheetTests.csproj          Project test Revit
   artifacts/                     Workbook và log kiểm thử
   bin/, obj/                     File build test sinh tự động
 docs/
   ARCHITECTURE.txt                Luồng hoạt động và cách sửa code
   HISTORY.txt                     Hướng dẫn/kết quả các phiên bản trước
 bin/Release/Revit2023/            DLL chính đã build
 obj/                             File trung gian sinh tự động

CÁCH DÙNG
1. Project nguồn: chạy tool > màn hình tải > Sheet List > Xuất dữ liệu > Excel hoặc JSON.
2. Đóng tool, mở hoặc tạo project đích trong Revit.
3. Chạy lại tool > Sheet List > Nhập / Tạo sheet > Nạp Excel / JSON > kiểm tra bảng xem trước.
4. Chọn title block đã có trong project đích, hoặc Không có title block.
5. Bấm Tạo sheet trong project > xác nhận. Có thể Undo trong Revit.
Tạo thủ công: Nhập / Tạo sheet > Tạo sheet thủ công > nhập Sheet Number/Sheet Name,
tick Placeholder nếu cần > Thêm vào danh sách > chọn title block > Tạo sheet trong project.
Có thể sửa số/tên hoặc tick Placeholder trực tiếp trên bản nháp trước khi tạo.
Xuất dữ liệu khi đang xem bản nháp sẽ xuất bản nháp đó, gồm cả sheet chưa tạo.
Xuất dữ liệu khi xem danh sách project sẽ xuất snapshot đang hiển thị; bấm Cập nhật để lấy dữ liệu mới nhất.
Sheet hiện hữu chỉ đọc; tick Placeholder dành cho nhập/tạo mới, không chuyển loại sheet hiện hữu.

Excel yêu cầu SheetNumber và SheetName ở dòng đầu. Cột IsPlaceholder là tùy chọn,
TRUE/FALSE hoặc 1/0. Ưu tiên worksheet Sheets, nếu không có đọc worksheet đầu tiên.
Giữ SheetNumber dạng Text để giữ số 0 đầu; không dùng công thức trong ô import.
Số sheet đã có được bỏ qua. Dữ liệu lỗi khóa nút tạo. Lỗi tạo sheet rollback toàn bộ.
Tool chỉ chuyển danh sách sheet; không chuyển views, title block family hoặc tham số khác.

MODELESS
Bạn có thể thao tác Revit khi tool mở. Đọc/ghi model đi qua ExternalEvent.
Revit xử lý yêu cầu sau khi kết thúc lệnh/chế độ chỉnh sửa đang chạy.
Tool gắn với project lúc mở; đổi project thì đóng tool và chạy lại trong project đích.
Danh sách hiện tại cập nhật sheet và title block sau thay đổi bên ngoài tool.
Đóng tool khi yêu cầu đang chờ sẽ hủy thao tác của yêu cầu đó.
Hộp chọn file và xác nhận vẫn cần tương tác; transaction có thể làm Revit bận ngắn.

PHIÊN BẢN VÀ KIỂM THỬ
DLL chính dành cho Revit 2023, .NET Framework 4.8, x64.
Revit 2024: .\build.ps1 -RevitVersion 2024, chưa chạy thực tế.
Revit 2025/2026 cần target .NET 8 riêng.
Lượt cập nhật 04/10/2026: build và chạy 36 kiểm thử Excel, JSON, layout và render WPF ở nền đều đạt.
Bộ test Revit đã build lại, gồm tạo sheet từ JSON; chưa chạy lại phần UI/tạo sheet mới trong Revit ở lượt này.
Kết quả chạy Revit của phiên bản trước: docs/HISTORY.txt và tests/artifacts/RevitTestResults.txt.
Không sửa các file trong bin/obj; chúng được sinh ra từ mã nguồn.

CÁC TAB
- Sheet List: danh sách sheet và quy trình export/import/tạo sheet hiện có.
- View List: mỗi sheet có ô chọn Floor Plan, vị trí X/Y và thao tác Preview/Lưu/Nạp/Áp dụng.
- Revision List: Sequence trong project, Description, Date và Issued.
- Placeholder Sheets: chỉ các sheet placeholder hiện hữu; tạo mới qua Sheet List/Excel.
Nút Cập nhật đọc lại dữ liệu qua ExternalEvent. Chuyển tab dùng dữ liệu đã nạp,
không gọi Revit API trực tiếp từ sự kiện chuyển tab. Ctrl+A/Ctrl+C sao chép bảng.
Revision Sequence không phải số revision riêng của từng sheet.
Bản có tabs đã build và test Excel; chưa test UI trực tiếp trong Revit.

CHỌN FLOOR PLAN THEO SHEET (VIEW LIST)
View List hiện là bảng Sheet Number, Sheet Name, Floor Plan để đặt,
Floor Plan hiện có và Trạng thái. Mỗi sheet thường có một ô chọn plan mới.
Liệt kê floor plan không phải template, chưa đặt hoặc đang nằm trên chính sheet tương ứng.
1. Mở View List, chọn Floor Plan trong ô tương ứng mỗi sheet cần đặt.
2. Bấm Đặt Floor Plan lên sheet và xác nhận project.
3. Tool kiểm tra lại sheet/view và đưa tâm Scope Box của Floor Plan về tâm vùng giấy chừa trái 44 mm, phải 94 mm (85 + 9).
Nếu cùng plan được chọn cho nhiều sheet, hoặc plan không thể đặt, chưa tạo viewport nào.
Nếu lỗi trong transaction, rollback toàn bộ lần đặt. Có thể Undo sau khi thành công.
Plan đã có trên sheet sẽ được canh lại, không tạo viewport trùng. Viewport pinned cần unpin trước.
Sau khi đặt, dùng Revit để chỉnh viewport, crop, scale và tránh chồng lên view khác.
Bấm Cập nhật để đọc lại model; thao tác này xóa các lựa chọn chưa áp dụng.
Plan phải tồn tại trong project đích. Chức năng này chưa tự duplicate hoặc tạo floor plan.
Bản mới đã build, 8 test Excel đạt. Thêm 6 kiểm thử viewport vào RevitSheetTests
và build thành công; các kiểm thử viewport chưa chạy trong Revit.



PREVIEW, LƯU VÀ NẠP TỌA ĐỘ
1. Tab View List: chọn Floor Plan tương ứng cho từng sheet cần bố trí.
2. Bấm Preview bố trí. Chọn sheet trong danh sách ở đầu cửa sổ preview.
3. Kéo ảnh/khung viewport hoặc nhập X (mm), Y (mm), bấm Cập nhật X/Y.
   Canh tâm Scope Box: tính lại vị trí viewport để tâm scope nằm giữa vùng giấy, xóa độ dịch thủ công.
4. Dùng vị trí và đóng: đưa tọa độ về bảng, chưa ghi viewport vào Revit.
   Lưu file bố trí: lưu toàn bộ bố trí đang preview vào XML, đồng thời cập nhật bảng.
5. Tại View List bấm Áp dụng Floor Plan để tạo viewport mới hoặc di chuyển viewport đang có.
6. Có thể Lưu bố trí từ View List, hoặc Nạp bố trí từ XML rồi Preview/Áp dụng lần sau.

Đơn vị file là mm. X/Y hiển thị là tâm viewport theo gốc sheet. Bố trí mới lưu scopeCentered cùng offsetX/offsetY so với vị trí canh scope.
Khi áp dụng, tool đọc lại scope của view và tính lại tâm viewport; tọa độ tuyệt đối đã lưu chỉ là snapshot.
XML cũ không có scopeCentered vẫn giữ tọa độ tuyệt đối. Muốn đổi: Preview > Canh tâm Scope Box > Dùng/Lưu > Áp dụng.
Tool chuyển mm sang feet trước khi gọi Revit API. Cho phép tọa độ âm nếu sheet dùng gốc khác.
File lưu UniqueId của sheet/view để khớp sau đổi tên. Nếu dùng project khác,
tool khớp bằng Sheet Number và View Name; các sheet/view đó phải đã tồn tại.
File sai đơn vị, tọa độ không hữu hạn, trùng hoặc không tìm thấy mục sẽ bị từ chối.
Nạp bố trí không thay đổi Revit. Đổi Floor Plan trong ô chọn sẽ bỏ tọa độ của lựa chọn cũ.
Bấm Cập nhật sẽ xóa các lựa chọn/vị trí chưa lưu; lưu XML trước nếu muốn giữ lại.

PREVIEW THỂ HIỆN GÌ
- Kích thước giấy từ sheet.Outline và vùng chừa trái 44 mm và vùng khung tên/khung ngoài 94 mm ở bên phải.
- Khung viewport đo bằng GetBoxOutline (không gồm nhãn viewport).
- Ảnh floor plan được export từ Revit nếu export thành công; nếu không, hiển thị khung và tên plan.
- Preview không phải ảnh đầy đủ của cả sheet: chưa vẽ family title block, schedule hoặc mọi viewport khác.
- Khung màu đỏ cảnh báo vượt vùng giấy/đè vùng chừa trái 44 mm hoặc phải 94 mm; người dùng vẫn có thể lưu vị trí đó.
- Chưa tự đổi crop/scale hay tự tránh va chạm. Kiểm tra bố trí trong Revit sau khi áp dụng.
- Sheet cần vùng giấy hợp lệ, thường cần có title block. Nếu không xác định được vùng giấy, preview báo lỗi.
- Đo viewport mới bằng transaction tạm và rollback; không giữ viewport mới từ thao tác Preview.
- Viewport pinned sẽ không được di chuyển; unpin trong Revit trước nếu cần.

KIỂM THỬ BẢN PREVIEW
.\test.ps1: build, 8 kiểm thử Excel, 12 kiểm thử lưu/nạp bố trí, 4 kiểm thử WPF preview ở nền đều đạt.
Ảnh tests/artifacts/LayoutPreview.png là bản render từ WPF với dữ liệu minh họa, không phải model Revit thực tế.
Bộ RevitSheetTests đã bổ sung kiểm tra di chuyển viewport và tọa độ mm, build thành công.
Chưa chạy trực tiếp phần export ảnh/đo viewport/áp dụng tọa độ trong Revit ở phiên làm việc này.


CẬP NHẬT LỀ 03/10/2026
Chừa trái 44 mm, phải 94 mm = 85 + 9 mm. CenterX = (MinX + 44 + MaxX - 94) / 2.
Với sheet 841 x 594 mm có MinX=MinY=0: tâm X=395.5 mm, Y=297 mm.
Tọa độ đã lưu trong XML vẫn giữ nguyên. Muốn tính lại theo lề mới: Nạp bố trí,
Preview > Canh tâm Scope Box cho sheet cần đổi > lưu lại > Áp dụng.

CANH THEO SCOPE BOX (03/10/2026)
- Floor Plan phải gán Scope Box trong Properties. Bảng View List hiển thị tên scope của lựa chọn.
- Mỗi zone dùng scope riêng. Tâm scope được chuyển model -> view projection -> sheet bằng API Revit 2023.
- Có tính tỷ lệ view và rotation của viewport. Không dùng tâm khung annotation để xác định tâm scope.
- Preview có dấu cộng xanh tại tâm scope. Kéo/nhập X/Y sẽ lưu độ dịch mm tương đối, mặc định là 0/0.
- Chưa hỗ trợ view có split crop. Báo lỗi trước Apply; lỗi phát sinh trong transaction sẽ rollback toàn bộ.
- Chưa gán scope: bố trí mới báo lỗi. File cũ có tọa độ tuyệt đối vẫn có thể áp dụng như trước.
- Canh scope không tự đổi scale/crop và không bảo đảm annotation không lấn khung tên; kiểm tra cảnh báo Preview.
- Test thực tế: Add-in Manager > tests/bin/RevitSheetExcelTests.dll > RevitScopeAlignmentTests.
  Cần project có Floor Plan gán scope, viewport unpinned trên sheet có title block.
  Test thử canh sau đổi vị trí/góc xoay và thử offset; rollback toàn bộ thay đổi trong model.
  Bộ test này đã build nhưng chưa chạy trong Revit ở lượt sửa này.

VIEW LIST · CẬP NHẬT 04/10/2026
- Bỏ cột Scope Box và Trạng thái. Vị trí chuyển lên ô phía trên bảng, theo sheet đang chọn.
- Ưu tiên scope của Floor Plan. Không có scope thì lấy tâm GetBoxCenter của viewport để canh giữa vùng giấy.
- View chưa đặt: tạo viewport tạm trong Preview, đo rồi rollback; khi Apply tạo viewport thật và canh lại.
- Nút Review Title Blocks nằm cạnh Cập nhật. Nếu có plan được chọn: review khung tên + plan và chỉnh/lưu tâm.
- Nếu chưa chọn plan: review riêng khung tên của dòng sheet đang chọn, dấu cộng xanh chỉ tâm vùng giấy.
- Khung tên review từ geometry thật của family instance trên sheet (kể cả vị trí/góc xoay), chỉ thể hiện đường nét; chưa vẽ chữ/logo.
- Nút Canh giữa tự kiểm tra scope/viewport ở ngầm. Lề giữ 44 mm trái, 94 mm phải.
- XML scopeCentered=true hiện mang nghĩa canh tự động + offset, bao gồm cả fallback khi scope không còn tồn tại.
- XML legacy vẫn dùng tọa độ tuyệt đối. Canh giữa trong Review rồi lưu để chuyển sang canh tự động.
- Không có scope không còn là lỗi. View có scope và split crop vẫn báo lỗi vì không xác định được một phép chiếu duy nhất.
- Lỗi Apply hiển thị sheet number và nguyên nhân trong thông báo; không cần cột Trạng thái.
- Tests nền đã qua; geometry title block và nhánh fallback trong model thật chưa chạy ở lượt này.

SEARCH, PROJECT BROWSER, RUNNING/DONE VÀ XÓA SHEET
- Màn hình tải đổi câu: Sắp xếp bản vẽ · Sẵn sàng cho ý tưởng mới.
- Sheet List và View List có Search theo Sheet Number/Sheet Name, không phân biệt hoa thường.
- Filter Project Browser: chọn toàn bộ sheet đang qua bộ lọc Browser Organization, hoặc nhóm đầy đủ đang có trong browser.
- Không đọc trạng thái expand/collapse/selection của cây browser; dùng BrowserOrganization của phần Sheets qua API.
- Bấm Cập nhật để đọc lại organization và các nhóm khi cấu hình Project Browser thay đổi.
- Nạp project và nạp Excel/JSON chạy từng bước ExternalEvent: Waiting -> Running -> Done, hiển thị từng dòng và thanh trạng thái.
- Nạp xong mới mở màn hình chính. Trạng thái kiểm tra dữ liệu nhập (Sẵn sàng/Lỗi/Bỏ qua) vẫn riêng với trạng thái xử lý.
- Xóa sheet: chọn dòng trong Sheet List/View List (Ctrl/Shift để chọn nhiều) > Xóa sheet > xác nhận số/tên sheet.
- Xóa sheet thực trong một transaction; kiểm tra ID/UniqueId và chặn sheet đang là active view. Có thể Undo trong Revit.
- Khi xem bản nháp, Xóa sheet chỉ bỏ dòng được chọn khỏi bản nháp, chưa xóa model.
- Xóa khỏi project có thể xóa title block/viewport/annotation gắn với sheet. Các floor plan độc lập không bị yêu cầu xóa.
- Kiểm thử search/filter/property notification và render nền đã qua. Test xóa trong Revit đã build, chưa chạy trên model thật ở lượt này.

TRẠNG THÁI CHỈ Ở STEP 2
- Bỏ quét Waiting/Running/Done khi mở tool, Cập nhật project hoặc nạp Excel/JSON.
- Step 1 giữ màn hình ảnh AI/progress indeterminate và kiểm tra dữ liệu nhập như trước.
- Chỉ View List có cột Trạng thái đặt Floor Plan. Trước khi áp dụng, trạng thái trống.
- Bấm Áp dụng Floor Plan: kiểm tra toàn bộ lựa chọn trước, rồi xử lý lần lượt Waiting -> Running -> Done.
- Done chỉ sau commit thành công. Error khi lỗi; Skipped khi bỏ qua; Cancelled cho dòng chưa xử lý nếu đóng tool/đổi project.
- Từng sheet dùng một transaction riêng, không giữ transaction qua ExternalEvent. Sheet đã Done được giữ nếu dòng khác lỗi; Undo riêng từng sheet.
- Sau hoàn tất đọc lại lựa chọn floor plan và giữ trạng thái kết quả trong bảng.
- Tests nền đã qua; chưa chạy tiến độ mới trực tiếp trên model Revit ở lượt sửa này.
