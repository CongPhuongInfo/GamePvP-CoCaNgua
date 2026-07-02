Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Collections.Generic
Imports System.IO

Public Class Form1
    Inherits Form

    Private Const DEFAULT_PORT As Integer = 9988
    Private Const BOARD_SIZE As Integer = 560
    Private Const HOME_MARGIN As Integer = 90    ' kich thuoc vung chuong o moi goc
    Private Const TOKEN_RADIUS As Integer = 13

    ' Mau tung nguoi choi: 0=Do, 1=Xanh la, 2=Vang, 3=Xanh duong
    Private Shared ReadOnly PlayerColor() As Color = {Color.FromArgb(220, 50, 47), Color.FromArgb(46, 139, 87), Color.FromArgb(218, 165, 32), Color.FromArgb(30, 100, 200)}
    Private Shared ReadOnly PlayerNameVN() As String = {"Do", "Xanh la", "Vang", "Xanh duong"}

    Private game As CaNguaGame
    Private isHost As Boolean
    Private localSeat As Integer = -1

    Private peer As NetworkPeer       ' dung khi la Client, ket noi den Host
    Private hub As NetworkHub         ' dung khi la Host, quan ly toi da 3 Client

    ' === UI ket noi / lobby ===
    Private pnlConnect As Panel
    Private txtPort As TextBox
    Private txtIP As TextBox
    Private btnHost As Button
    Private btnJoin As Button
    Private lblStatus As Label
    Private btnStartGame As Button
    Private lstLobby As ListBox

    ' === UI game ===
    Private pnlGame As Panel
    Private boardPanel As Panel
    Private lblTurn As Label
    Private lblYouAre As Label
    Private btnRoll As Button
    Private btnPass As Button
    Private btnRestart As Button
    Private pnlPlayers(3) As Panel
    Private lblCardStatus(3) As Label
    Private lblCardStats(3) As Label

    ' === Khung chat (luon hien khi dang choi, du 2/3/4 nguoi) ===
    Private pnlChat As Panel
    Private lstChat As ListBox
    Private txtChatInput As TextBox
    Private btnSend As Button

    ' Vung click cua tung quan, duoc tinh lai moi lan ve board
    Private Structure TokenHitArea
        Public Rect As Rectangle
        Public Player As Integer
        Public TokenIdx As Integer
    End Structure
    Private tokenHitAreas As New List(Of TokenHitArea)()

    ' === Sprite (co fallback ve hinh khoi neu thieu file, giong MineForm.vb) ===
    Private horseSprite(3) As Image             ' anh ngua theo tung player, Nothing neu thieu file
    Private diceSprite(6) As Image               ' anh xuc xac mat 1..6 (index 0 khong dung)
    Private spritesLoaded As Boolean = False

    ' === Xuc xac quay 6 mat khi do ===
    Private picDice As Panel
    Private diceAnimTimer As System.Windows.Forms.Timer
    Private diceAnimStep As Integer = 0
    Private diceAnimTargetValue As Integer = 1
    Private diceFaceShown As Integer = 0          ' 0 = chua do (trang thai cho)
    Private diceAnimRng As New Random()
    Private wasDiceRolledPrev As Boolean = False
    Private Const DICE_ANIM_STEPS As Integer = 9
    Private Const DICE_ANIM_INTERVAL_MS As Integer = 70

    Public Sub New()
        LoadSprites()
        InitUI()
    End Sub

    ' ============================================================
    '  LOAD SPRITE (co fallback ve hinh khoi neu thieu file)
    ' ============================================================
    Private Sub LoadSprites()
        Try
            Dim dir As String = Path.Combine(Application.StartupPath, "Assets")
            horseSprite(0) = LoadImg(dir, "horse_do.png")
            horseSprite(1) = LoadImg(dir, "horse_xanhla.png")
            horseSprite(2) = LoadImg(dir, "horse_vang.png")
            horseSprite(3) = LoadImg(dir, "horse_xanhduong.png")
            Dim v As Integer
            For v = 1 To 6
                diceSprite(v) = LoadImg(dir, "dice_" & v.ToString() & ".png")
            Next v
            spritesLoaded = True
        Catch ex As Exception
            spritesLoaded = False
        End Try
    End Sub

    Private Function LoadImg(dir As String, name As String) As Image
        Dim p As String = Path.Combine(dir, name)
        If File.Exists(p) Then Return Image.FromFile(p)
        Return Nothing
    End Function

    Private Sub InitUI()
        Me.Text = "Co Ca Ngua Online - 2CongLC"
        Me.ClientSize = New Size(900, 660)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(245, 245, 240)

        BuildConnectPanel()
        BuildGamePanel()
        pnlGame.Visible = False
    End Sub

    ' ============================================================
    '  CONNECT / LOBBY PANEL
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Dock = DockStyle.Fill
        pnlConnect.BackColor = Color.FromArgb(245, 245, 240)

        Dim lbl As New Label()
        lbl.Text = "CO CA NGUA ONLINE (4 nguoi choi)"
        lbl.Font = New Font("Segoe UI", 16.0!, FontStyle.Bold)
        lbl.Location = New Point(220, 50) : lbl.AutoSize = True
        pnlConnect.Controls.Add(lbl)

        Dim lPort As New Label() : lPort.Text = "Port:" : lPort.Location = New Point(340, 110) : lPort.AutoSize = True
        pnlConnect.Controls.Add(lPort)
        txtPort = New TextBox() : txtPort.Text = DEFAULT_PORT.ToString() : txtPort.Location = New Point(400, 107) : txtPort.Width = 80
        pnlConnect.Controls.Add(txtPort)

        btnHost = New Button() : btnHost.Text = "Tao phong (Host - toi da 4 nguoi)"
        btnHost.Location = New Point(300, 145) : btnHost.Size = New Size(280, 36)
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        Dim lIP As New Label() : lIP.Text = "IP Host:" : lIP.Location = New Point(340, 200) : lIP.AutoSize = True
        pnlConnect.Controls.Add(lIP)
        txtIP = New TextBox() : txtIP.Text = "127.0.0.1" : txtIP.Location = New Point(410, 197) : txtIP.Width = 140
        pnlConnect.Controls.Add(txtIP)

        btnJoin = New Button() : btnJoin.Text = "Vao phong (Khach)"
        btnJoin.Location = New Point(300, 235) : btnJoin.Size = New Size(280, 36)
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lstLobby = New ListBox()
        lstLobby.Location = New Point(300, 290) : lstLobby.Size = New Size(280, 110)
        pnlConnect.Controls.Add(lstLobby)

        btnStartGame = New Button() : btnStartGame.Text = "Bat dau choi"
        btnStartGame.Location = New Point(300, 410) : btnStartGame.Size = New Size(280, 36)
        btnStartGame.Visible = False
        AddHandler btnStartGame.Click, AddressOf BtnStartGame_Click
        pnlConnect.Controls.Add(btnStartGame)

        lblStatus = New Label() : lblStatus.Location = New Point(220, 460) : lblStatus.AutoSize = True
        lblStatus.ForeColor = Color.DimGray
        lblStatus.Text = "Host: bam 'Tao phong', cho nguoi khac vao roi bam 'Bat dau choi'." & Environment.NewLine & "Khach: nhap IP roi bam 'Vao phong'."
        pnlConnect.Controls.Add(lblStatus)

        Me.Controls.Add(pnlConnect)
    End Sub

    ' ============================================================
    '  GAME PANEL
    ' ============================================================
    Private Sub BuildGamePanel()
        pnlGame = New Panel()
        pnlGame.Dock = DockStyle.Fill
        pnlGame.BackColor = Color.FromArgb(245, 245, 240)

        boardPanel = New Panel()
        boardPanel.Location = New Point(20, 20)
        boardPanel.Size = New Size(BOARD_SIZE, BOARD_SIZE)
        boardPanel.BackColor = Color.White
        boardPanel.BorderStyle = BorderStyle.FixedSingle
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        AddHandler boardPanel.MouseClick, AddressOf BoardPanel_MouseClick
        pnlGame.Controls.Add(boardPanel)

        Dim sideX As Integer = BOARD_SIZE + 40

        lblYouAre = New Label() : lblYouAre.Location = New Point(sideX, 20) : lblYouAre.AutoSize = True
        lblYouAre.Font = New Font("Segoe UI", 10.0!, FontStyle.Bold)
        pnlGame.Controls.Add(lblYouAre)

        lblTurn = New Label() : lblTurn.Location = New Point(sideX, 50) : lblTurn.AutoSize = True
        lblTurn.Font = New Font("Segoe UI", 10.0!)
        pnlGame.Controls.Add(lblTurn)

        picDice = New Panel()
        picDice.Location = New Point(sideX, 85) : picDice.Size = New Size(64, 64)
        AddHandler picDice.Paint, AddressOf PicDice_Paint
        pnlGame.Controls.Add(picDice)

        btnRoll = New Button() : btnRoll.Text = "Do xuc xac"
        btnRoll.Location = New Point(sideX, 157) : btnRoll.Size = New Size(140, 36)
        AddHandler btnRoll.Click, AddressOf BtnRoll_Click
        pnlGame.Controls.Add(btnRoll)

        btnPass = New Button() : btnPass.Text = "Bo luot"
        btnPass.Location = New Point(sideX + 150, 157) : btnPass.Size = New Size(110, 36)
        btnPass.Visible = False
        AddHandler btnPass.Click, AddressOf BtnPass_Click
        pnlGame.Controls.Add(btnPass)

        Dim p As Integer
        For p = 0 To 3
            pnlPlayers(p) = BuildPlayerCard(p, New Point(sideX, 201 + p * 64), 290)
            pnlGame.Controls.Add(pnlPlayers(p))
        Next p

        btnRestart = New Button() : btnRestart.Text = "Choi lai (Host)"
        btnRestart.Location = New Point(sideX, 459) : btnRestart.Size = New Size(290, 30)
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        pnlGame.Controls.Add(btnRestart)

        BuildChatPanel(sideX, 290, 497, 660 - 497 - 15)

        Me.Controls.Add(pnlGame)
    End Sub

    ''' <summary>Tao 1 the (card) thong tin nguoi choi: thanh mau ben trai + ten + trang thai +
    ''' dong thong ke (so quan da ve dich / so quan con trong chuong).</summary>
    Private Function BuildPlayerCard(player As Integer, loc As Point, w As Integer) As Panel
        Dim card As New Panel()
        card.Location = loc : card.Size = New Size(w, 58)
        card.BackColor = Color.White
        card.BorderStyle = BorderStyle.FixedSingle

        Dim bar As New Panel()
        bar.Location = New Point(0, 0) : bar.Size = New Size(6, 58)
        bar.BackColor = PlayerColor(player)
        card.Controls.Add(bar)

        Dim lblTitle As New Label()
        lblTitle.Text = "Player " & (player + 1).ToString() & " (" & PlayerNameVN(player) & ")"
        lblTitle.Font = New Font("Segoe UI", 9.5!, FontStyle.Bold)
        lblTitle.ForeColor = PlayerColor(player)
        lblTitle.Location = New Point(16, 4) : lblTitle.AutoSize = True
        card.Controls.Add(lblTitle)

        lblCardStatus(player) = New Label()
        lblCardStatus(player).Text = "trong"
        lblCardStatus(player).Font = New Font("Segoe UI", 9.0!)
        lblCardStatus(player).ForeColor = Color.DimGray
        lblCardStatus(player).Location = New Point(16, 22) : lblCardStatus(player).AutoSize = True
        card.Controls.Add(lblCardStatus(player))

        lblCardStats(player) = New Label()
        lblCardStats(player).Text = ""
        lblCardStats(player).Font = New Font("Segoe UI", 8.0!)
        lblCardStats(player).ForeColor = Color.Gray
        lblCardStats(player).Location = New Point(16, 39) : lblCardStats(player).AutoSize = True
        card.Controls.Add(lblCardStats(player))

        Return card
    End Function

    ''' <summary>Khung chat: ListBox hien tin nhan (ca chat va log he thong) + TextBox go + nut Gui.</summary>
    Private Sub BuildChatPanel(x As Integer, w As Integer, y As Integer, h As Integer)
        pnlChat = New Panel()
        pnlChat.Location = New Point(x, y)
        pnlChat.Size = New Size(w, h)

        lstChat = New ListBox()
        lstChat.Location = New Point(0, 0)
        lstChat.Size = New Size(w, h - 30)
        pnlChat.Controls.Add(lstChat)

        txtChatInput = New TextBox()
        txtChatInput.Location = New Point(0, h - 26)
        txtChatInput.Size = New Size(w - 55, 24)
        AddHandler txtChatInput.KeyDown, Sub(s As Object, ev As KeyEventArgs)
            If ev.KeyCode = Keys.Enter Then
                BtnSend_Click(s, EventArgs.Empty)
                ev.Handled = True
                ev.SuppressKeyPress = True
            End If
        End Sub
        pnlChat.Controls.Add(txtChatInput)

        btnSend = New Button()
        btnSend.Text = "Gui"
        btnSend.Location = New Point(w - 50, h - 27)
        btnSend.Size = New Size(50, 26)
        AddHandler btnSend.Click, AddressOf BtnSend_Click
        pnlChat.Controls.Add(btnSend)

        pnlGame.Controls.Add(pnlChat)
    End Sub

    Private Sub BtnSend_Click(sender As Object, e As EventArgs)
        If txtChatInput.Text.Trim() = "" Then Return
        If localSeat < 0 Then Return
        Dim tag As String = "Player " & (localSeat + 1).ToString()
        Dim msg As String = txtChatInput.Text.Trim()
        AppendChat(tag & ": " & msg)

        If isHost Then
            If hub IsNot Nothing Then hub.Broadcast("CHAT:" & tag & ":" & msg)
        Else
            If peer IsNot Nothing AndAlso peer.IsConnected Then peer.SendLine("CHAT:" & tag & ":" & msg)
        End If

        txtChatInput.Text = ""
        txtChatInput.Focus()
    End Sub

    Private Sub AppendChat(msg As String)
        If lstChat Is Nothing Then Return
        lstChat.Items.Add(msg)
        lstChat.TopIndex = Math.Max(0, lstChat.Items.Count - 1)
    End Sub

    ' ============================================================
    '  XUC XAC QUAY 6 MAT (chi hieu ung hinh anh, ket qua da co san)
    ' ============================================================
    ''' <summary>Bat dau hieu ung quay: doi nhanh qua cac mat ngau nhien roi dung lai
    ''' dung o gia tri thuc te (targetValue) da duoc xuc xac quyet dinh.</summary>
    Private Sub StartDiceAnim(targetValue As Integer)
        diceAnimTargetValue = targetValue
        diceAnimStep = 0
        If diceAnimTimer Is Nothing Then
            diceAnimTimer = New System.Windows.Forms.Timer()
            diceAnimTimer.Interval = DICE_ANIM_INTERVAL_MS
            AddHandler diceAnimTimer.Tick, AddressOf DiceAnimTimer_Tick
        End If
        diceFaceShown = diceAnimRng.Next(1, 7)
        picDice.Invalidate()
        diceAnimTimer.Start()
    End Sub

    Private Sub DiceAnimTimer_Tick(sender As Object, e As EventArgs)
        diceAnimStep += 1
        If diceAnimStep >= DICE_ANIM_STEPS Then
            diceAnimTimer.Stop()
            diceFaceShown = diceAnimTargetValue
        Else
            diceFaceShown = diceAnimRng.Next(1, 7)
        End If
        picDice.Invalidate()
    End Sub

    Private Sub PicDice_Paint(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim rect As New Rectangle(0, 0, picDice.Width, picDice.Height)
        Dim accent As Color = If(game IsNot Nothing, PlayerColor(game.CurrentPlayer), Color.DimGray)

        If diceFaceShown >= 1 AndAlso diceFaceShown <= 6 AndAlso spritesLoaded AndAlso diceSprite(diceFaceShown) IsNot Nothing Then
            g.DrawImage(diceSprite(diceFaceShown), rect)
        ElseIf diceFaceShown >= 1 AndAlso diceFaceShown <= 6 Then
            ' Fallback: khong co sprite, ve khoi trang + so
            Using b As New SolidBrush(Color.White)
                g.FillRectangle(b, rect)
            End Using
            Using pen As New Pen(accent, 3)
                g.DrawRectangle(pen, 1, 1, rect.Width - 2, rect.Height - 2)
            End Using
            Using sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                Using fnt As New Font("Segoe UI", 26.0!, FontStyle.Bold)
                    Using textBrush As New SolidBrush(accent)
                        g.DrawString(diceFaceShown.ToString(), fnt, textBrush, New RectangleF(rect.X, rect.Y, rect.Width, rect.Height), sf)
                    End Using
                End Using
            End Using
        Else
            ' Chua do: khung cho voi dau "-"
            Using pen As New Pen(Color.LightGray, 2)
                g.DrawRectangle(pen, 1, 1, rect.Width - 2, rect.Height - 2)
            End Using
            Using sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                Using fnt As New Font("Segoe UI", 22.0!, FontStyle.Bold)
                    Using textBrush As New SolidBrush(Color.LightGray)
                        g.DrawString("-", fnt, textBrush, New RectangleF(rect.X, rect.Y, rect.Width, rect.Height), sf)
                    End Using
                End Using
            End Using
        End If
    End Sub

    ' ============================================================
    '  VE BAN CO (GDI+)
    ' ============================================================
    Private Function TrackRect() As Rectangle
        Return New Rectangle(HOME_MARGIN, HOME_MARGIN, BOARD_SIZE - 2 * HOME_MARGIN, BOARD_SIZE - 2 * HOME_MARGIN)
    End Function

    ''' <summary>Diem tren duong vong (52 o), danh deu quanh chu vi TrackRect, theo chieu kim dong ho, bat dau goc tren-trai.</summary>
    Private Function TrackPoint(squareIndex As Integer) As PointF
        Dim r As Rectangle = TrackRect()
        Dim w As Double = r.Width
        Dim h As Double = r.Height
        Dim perim As Double = 2.0 * (w + h)
        Dim dist As Double = (squareIndex Mod CaNguaGame.TRACK_LEN) / CDbl(CaNguaGame.TRACK_LEN) * perim

        If dist < w Then
            Return New PointF(CSng(r.Left + dist), CSng(r.Top))
        ElseIf dist < w + h Then
            Return New PointF(CSng(r.Right), CSng(r.Top + (dist - w)))
        ElseIf dist < 2 * w + h Then
            Return New PointF(CSng(r.Right - (dist - w - h)), CSng(r.Bottom))
        Else
            Return New PointF(CSng(r.Left), CSng(r.Bottom - (dist - 2 * w - h)))
        End If
    End Function

    Private Function BoardCenter() As PointF
        Return New PointF(BOARD_SIZE / 2.0F, BOARD_SIZE / 2.0F)
    End Function

    ''' <summary>Diem tren duong ve nha rieng (local pos 51..56), noi tu o xuat phat vao trung tam.</summary>
    Private Function HomeStretchPoint(player As Integer, localPos As Integer) As PointF
        Dim startPt As PointF = TrackPoint(player * 13)
        Dim centerPt As PointF = BoardCenter()
        Dim stepIdx As Integer = localPos - 51 ' 0..5
        Dim t As Double = (stepIdx + 1) / 7.0
        Return New PointF(CSng(startPt.X + (centerPt.X - startPt.X) * t), CSng(startPt.Y + (centerPt.Y - startPt.Y) * t))
    End Function

    ''' <summary>Diem hien thi quan da ve dich (local pos = 57), xep gan trung tam theo tung nguoi.</summary>
    Private Function FinishPoint(player As Integer, tokenIdx As Integer) As PointF
        Dim c As PointF = BoardCenter()
        Dim angle As Double = (player * 90 + tokenIdx * 20) * Math.PI / 180.0
        Dim radius As Double = 14
        Return New PointF(CSng(c.X + radius * Math.Cos(angle)), CSng(c.Y + radius * Math.Sin(angle)))
    End Function

    ''' <summary>Diem trong chuong (chua xuat phat), xep 2x2 trong moi vung goc.</summary>
    Private Function BaseSlotPoint(player As Integer, tokenIdx As Integer) As PointF
        Dim cx As Single, cy As Single
        Select Case player
            Case 0 : cx = HOME_MARGIN * 0.5F : cy = HOME_MARGIN * 0.5F
            Case 1 : cx = BOARD_SIZE - HOME_MARGIN * 0.5F : cy = HOME_MARGIN * 0.5F
            Case 2 : cx = BOARD_SIZE - HOME_MARGIN * 0.5F : cy = BOARD_SIZE - HOME_MARGIN * 0.5F
            Case Else : cx = HOME_MARGIN * 0.5F : cy = BOARD_SIZE - HOME_MARGIN * 0.5F
        End Select
        Dim dx As Single = If(tokenIdx Mod 2 = 0, -18, 18)
        Dim dy As Single = If(tokenIdx < 2, -18, 18)
        Return New PointF(cx + dx, cy + dy)
    End Function

    ''' <summary>Tra ve toa do man hinh hien thi cua mot quan, tuy theo localPos.</summary>
    Private Function TokenScreenPoint(player As Integer, tokenIdx As Integer) As PointF
        If game Is Nothing Then Return New PointF(0, 0)
        Dim pos As Integer = game.TokenPos(player, tokenIdx)
        If pos = CaNguaGame.POS_BASE Then
            Return BaseSlotPoint(player, tokenIdx)
        ElseIf pos = CaNguaGame.POS_FINISH Then
            Return FinishPoint(player, tokenIdx)
        ElseIf pos >= 51 Then
            Return HomeStretchPoint(player, pos)
        Else
            Dim sq As Integer = game.GlobalSquare(player, pos)
            Dim basePt As PointF = TrackPoint(sq)
            ' lech nhe de khong de chong khi nhieu quan cung o
            Dim jx As Single = (player Mod 2) * 6 - 3
            Dim jy As Single = (tokenIdx Mod 2) * 6 - 3
            Return New PointF(basePt.X + jx, basePt.Y + jy)
        End If
    End Function

    Private Sub BoardPanel_Paint(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.Clear(Color.FromArgb(250, 248, 240))

        Dim r As Rectangle = TrackRect()

        ' 4 vung chuong (goc) theo mau nguoi choi
        Dim homeBoxes(3) As Rectangle
        homeBoxes(0) = New Rectangle(0, 0, HOME_MARGIN, HOME_MARGIN)
        homeBoxes(1) = New Rectangle(BOARD_SIZE - HOME_MARGIN, 0, HOME_MARGIN, HOME_MARGIN)
        homeBoxes(2) = New Rectangle(BOARD_SIZE - HOME_MARGIN, BOARD_SIZE - HOME_MARGIN, HOME_MARGIN, HOME_MARGIN)
        homeBoxes(3) = New Rectangle(0, BOARD_SIZE - HOME_MARGIN, HOME_MARGIN, HOME_MARGIN)
        Dim p As Integer
        For p = 0 To 3
            Using b As New SolidBrush(Color.FromArgb(40, PlayerColor(p)))
                g.FillRectangle(b, homeBoxes(p))
            End Using
            Using pen As New Pen(PlayerColor(p), 3)
                g.DrawRectangle(pen, homeBoxes(p))
            End Using
        Next p

        ' Vong duong di 52 o
        Dim i As Integer
        For i = 0 To CaNguaGame.TRACK_LEN - 1
            Dim pt As PointF = TrackPoint(i)
            Dim isSafe As Boolean = game IsNot Nothing AndAlso game.IsSafeSquare(i)
            Dim isStart As Integer = -1
            Dim sp As Integer
            For sp = 0 To 3
                If sp * 13 = i Then isStart = sp
            Next sp

            Dim cellColor As Color = Color.FromArgb(230, 230, 225)
            If isStart >= 0 Then
                cellColor = Color.FromArgb(120, PlayerColor(isStart))
            ElseIf isSafe Then
                cellColor = Color.FromArgb(255, 240, 200)
            End If

            Using b As New SolidBrush(cellColor)
                g.FillEllipse(b, pt.X - 9, pt.Y - 9, 18, 18)
            End Using
            Using pen As New Pen(Color.Gray, 1)
                g.DrawEllipse(pen, pt.X - 9, pt.Y - 9, 18, 18)
            End Using
        Next i

        ' 4 duong ve nha
        For p = 0 To 3
            Dim k As Integer
            For k = 51 To 56
                Dim hp As PointF = HomeStretchPoint(p, k)
                Using b As New SolidBrush(Color.FromArgb(150, PlayerColor(p)))
                    g.FillEllipse(b, hp.X - 7, hp.Y - 7, 14, 14)
                End Using
            Next k
        Next p

        ' Trung tam (dich)
        Dim c As PointF = BoardCenter()
        Using b As New SolidBrush(Color.FromArgb(255, 250, 230))
            g.FillEllipse(b, c.X - 26, c.Y - 26, 52, 52)
        End Using
        Using pen As New Pen(Color.DarkGoldenrod, 2)
            g.DrawEllipse(pen, c.X - 26, c.Y - 26, 52, 52)
        End Using

        ' Cac quan co
        tokenHitAreas.Clear()
        If game IsNot Nothing Then
            For p = 0 To 3
                If Not game.ActiveSeat(p) Then Continue For
                Dim t As Integer
                For t = 0 To CaNguaGame.TOKENS_PER_PLAYER - 1
                    Dim pt As PointF = TokenScreenPoint(p, t)
                    Dim rect As New Rectangle(CInt(pt.X - TOKEN_RADIUS), CInt(pt.Y - TOKEN_RADIUS), TOKEN_RADIUS * 2, TOKEN_RADIUS * 2)

                    Dim canClick As Boolean = (game.CurrentPlayer = localSeat) AndAlso (p = localSeat) AndAlso game.DiceRolled AndAlso game.CanMoveToken(p, t, game.DiceValue)
                    Dim isMyToken As Boolean = (p = localSeat)

                    ' Glow vang khi co the click
                    If canClick Then
                        Using glowBrush As New SolidBrush(Color.FromArgb(80, Color.Yellow))
                            g.FillEllipse(glowBrush, rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8)
                        End Using
                    End If

                    ' Than quan: dung sprite ngua neu co, khong thi fallback ve hinh tron nhu cu
                    If spritesLoaded AndAlso horseSprite(p) IsNot Nothing Then
                        g.DrawImage(horseSprite(p), rect)

                        If canClick OrElse isMyToken Then
                            Dim ringColor As Color = If(canClick, Color.Yellow, Color.White)
                            Dim ringWidth As Single = If(canClick, 3.0F, 1.5F)
                            Using pen As New Pen(ringColor, ringWidth)
                                g.DrawEllipse(pen, rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2)
                            End Using
                        End If

                        ' So thu tu quan: hien o goc duoi-phai bang 1 huy hieu tron nho, khong che mat ngua
                        Dim badgeSize As Integer = 13
                        Dim badgeRect As New Rectangle(rect.Right - badgeSize + 3, rect.Bottom - badgeSize + 3, badgeSize, badgeSize)
                        Using bb As New SolidBrush(Color.FromArgb(235, 255, 255, 255))
                            g.FillEllipse(bb, badgeRect)
                        End Using
                        Using bp As New Pen(PlayerColor(p), 1.0F)
                            g.DrawEllipse(bp, badgeRect)
                        End Using
                        Using sf2 As New StringFormat()
                            sf2.Alignment = StringAlignment.Center
                            sf2.LineAlignment = StringAlignment.Center
                            Using fnt2 As New Font("Arial", 6.5!, FontStyle.Bold)
                                Using tb2 As New SolidBrush(PlayerColor(p))
                                    g.DrawString((t + 1).ToString(), fnt2, tb2, New RectangleF(badgeRect.X, badgeRect.Y, badgeRect.Width, badgeRect.Height), sf2)
                                End Using
                            End Using
                        End Using
                    Else
                        Using b As New SolidBrush(PlayerColor(p))
                            g.FillEllipse(b, rect)
                        End Using

                        ' Vien: vang = co the di, trang = quan minh, xam = quan doi
                        Dim borderColor As Color = If(canClick, Color.Yellow, If(isMyToken, Color.White, Color.FromArgb(180, 180, 180)))
                        Dim borderWidth As Single = If(canClick, 3.0F, If(isMyToken, 1.5F, 1.0F))
                        Using pen As New Pen(borderColor, borderWidth)
                            g.DrawEllipse(pen, rect)
                        End Using

                        ' So thu tu quan (1-4)
                        Using sf As New StringFormat()
                            sf.Alignment = StringAlignment.Center
                            sf.LineAlignment = StringAlignment.Center
                            Using fnt As New Font("Arial", 7.0!, FontStyle.Bold)
                                Using textBrush As New SolidBrush(Color.White)
                                    g.DrawString((t + 1).ToString(), fnt, textBrush, New RectangleF(rect.X, rect.Y, rect.Width, rect.Height), sf)
                                End Using
                            End Using
                        End Using
                    End If

                    Dim hit As TokenHitArea
                    hit.Rect = rect
                    hit.Player = p
                    hit.TokenIdx = t
                    tokenHitAreas.Add(hit)
                Next t
            Next p
        End If
    End Sub

    Private Sub BoardPanel_MouseClick(sender As Object, e As MouseEventArgs)
        If game Is Nothing OrElse game.GameOver Then Return
        If game.CurrentPlayer <> localSeat Then Return
        If Not game.DiceRolled Then Return

        Dim i As Integer
        For i = tokenHitAreas.Count - 1 To 0 Step -1
            Dim h As TokenHitArea = tokenHitAreas(i)
            If h.Player = localSeat AndAlso h.Rect.Contains(e.Location) Then
                If game.CanMoveToken(h.Player, h.TokenIdx, game.DiceValue) Then
                    RequestMove(h.TokenIdx)
                Else
                    Dim pos As Integer = game.TokenPos(h.Player, h.TokenIdx)
                    If pos = CaNguaGame.POS_BASE Then
                        AppendLog("Quan " & (h.TokenIdx + 1).ToString() & " dang trong chuong, can do duoc 6 moi xuat quan.")
                    ElseIf pos = CaNguaGame.POS_FINISH Then
                        AppendLog("Quan " & (h.TokenIdx + 1).ToString() & " da ve dich roi.")
                    Else
                        AppendLog("Quan " & (h.TokenIdx + 1).ToString() & " khong the di " & game.DiceValue.ToString() & " buoc (se vuot dich).")
                    End If
                End If
                Exit For
            End If
        Next i
    End Sub

    ' ============================================================
    '  HANH DONG NGUOI CHOI (Roll / Move / Pass)
    ' ============================================================
    Private Sub BtnRoll_Click(sender As Object, e As EventArgs)
        If game Is Nothing OrElse game.GameOver Then Return
        If game.CurrentPlayer <> localSeat Then Return
        If game.DiceRolled Then Return

        If isHost Then
            ProcessRoll(localSeat)
        Else
            peer.SendLine("ROLLREQ")
        End If
    End Sub

    Private Sub BtnPass_Click(sender As Object, e As EventArgs)
        If game Is Nothing OrElse game.GameOver Then Return
        If game.CurrentPlayer <> localSeat Then Return
        If Not game.DiceRolled Then Return
        If game.HasAnyMove(localSeat, game.DiceValue) Then Return

        If isHost Then
            ProcessPass(localSeat)
        Else
            peer.SendLine("PASSREQ")
        End If
    End Sub

    Private Sub RequestMove(tokenIdx As Integer)
        If isHost Then
            ProcessMove(localSeat, tokenIdx)
        Else
            peer.SendLine("MOVEREQ:" & tokenIdx.ToString())
        End If
    End Sub

    ' ============================================================
    '  XU LY PHIA HOST (luon chay tren may Host, du la Host hay Client yeu cau)
    ' ============================================================
    Private Sub ProcessRoll(seat As Integer)
        If game.CurrentPlayer <> seat OrElse game.DiceRolled Then Return
        Dim v As Integer = game.RollDice()
        AppendLog("Player " & (seat + 1).ToString() & " do duoc " & v.ToString() & ".")
        SyncAfterChange()
    End Sub

    Private Sub ProcessMove(seat As Integer, tokenIdx As Integer)
        If game.CurrentPlayer <> seat Then Return
        Dim captured As Boolean = False
        Dim err As String = ""
        If game.TryMoveToken(seat, tokenIdx, captured, err) Then
            AppendLog(game.LastLog)
            SyncAfterChange()
            CheckAndShowGameOver()
        End If
    End Sub

    Private Sub ProcessPass(seat As Integer)
        If game.CurrentPlayer <> seat Then Return
        If game.HasAnyMove(seat, game.DiceValue) Then Return
        game.PassTurnNoMove()
        AppendLog(game.LastLog)
        SyncAfterChange()
    End Sub

    ''' <summary>Cap nhat UI cua Host va, neu la Host, gui STATE cho tat ca Client.</summary>
    Private Sub SyncAfterChange()
        RefreshUI()
        If isHost AndAlso hub IsNot Nothing Then
            hub.Broadcast("STATE:" & game.Serialize())
        End If
    End Sub

    ' ============================================================
    '  KET NOI MANG - HOST
    ' ============================================================
    Private Sub BtnHost_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return

        isHost = True
        localSeat = 0
        game = New CaNguaGame()
        game.ActiveSeat(0) = True

        hub = New NetworkHub(Me)
        AddHandler hub.ClientConnected, AddressOf Hub_ClientConnected
        AddHandler hub.ClientDisconnected, AddressOf Hub_ClientDisconnected
        AddHandler hub.LineReceivedFromClient, AddressOf Hub_LineReceivedFromClient

        Try
            hub.StartListening(port)
            lblStatus.Text = "Dang cho nguoi choi tren port " & port.ToString() & "... (Toi da 3 khach)"
            btnHost.Enabled = False : btnJoin.Enabled = False
            btnStartGame.Visible = True
            RefreshLobby()
        Catch ex As Exception
            MessageBox.Show("Loi: " & ex.Message)
        End Try
    End Sub

    Private Sub Hub_ClientConnected(seat As Integer)
        game.ActiveSeat(seat) = True
        hub.SendToClient(seat, "WELCOME:" & seat.ToString())
        RefreshLobby()
    End Sub

    Private Sub Hub_ClientDisconnected(seat As Integer)
        game.ActiveSeat(seat) = False
        RefreshLobby()
        If pnlGame.Visible Then
            AppendLog("Player " & (seat + 1).ToString() & " da mat ket noi.")
            If game.CurrentPlayer = seat Then game.MoveToNextActivePlayer()
            SyncAfterChange()
        End If
    End Sub

    Private Sub RefreshLobby()
        lstLobby.Items.Clear()
        Dim p As Integer
        For p = 0 To 3
            Dim status As String
            If p = 0 Then
                status = "Host (Ban)"
            ElseIf game.ActiveSeat(p) Then
                status = "Da vao phong"
            Else
                status = "Cho..."
            End If
            lstLobby.Items.Add("Player " & (p + 1).ToString() & " (" & PlayerNameVN(p) & "): " & status)
        Next p
    End Sub

    Private Sub BtnStartGame_Click(sender As Object, e As EventArgs)
        If game.ActivePlayerCount() < 2 Then
            MessageBox.Show("Can it nhat 2 nguoi choi de bat dau.")
            Return
        End If
        game.CurrentPlayer = game.FirstActiveSeat()
        ShowGamePanel()
        AppendLog("Bat dau game voi " & game.ActivePlayerCount().ToString() & " nguoi choi.")
        hub.Broadcast("STATE:" & game.Serialize())
    End Sub

    Private Sub Hub_LineReceivedFromClient(seat As Integer, line As String)
        If line.StartsWith("HELLO") Then
            ' Da gui WELCOME luc Connected, khong can lam gi them.
        ElseIf line.StartsWith("ROLLREQ") Then
            ProcessRoll(seat)
        ElseIf line.StartsWith("MOVEREQ:") Then
            Dim t As Integer
            If Integer.TryParse(line.Substring(8), t) Then ProcessMove(seat, t)
        ElseIf line.StartsWith("PASSREQ") Then
            ProcessPass(seat)
        ElseIf line.StartsWith("CHAT:") Then
            Dim payload As String = line.Substring(5)
            Dim colon As Integer = payload.IndexOf(":"c)
            If colon >= 0 Then
                Dim tag As String = payload.Substring(0, colon)
                Dim msg As String = payload.Substring(colon + 1)
                AppendChat(tag & ": " & msg)
            End If
            hub.BroadcastExcept(line, seat)
        End If
    End Sub

    ' ============================================================
    '  KET NOI MANG - CLIENT
    ' ============================================================
    Private Sub BtnJoin_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        If txtIP.Text.Trim() = "" Then MessageBox.Show("Nhap IP.") : Return

        isHost = False
        game = New CaNguaGame()
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected

        lblStatus.Text = "Dang ket noi..."
        btnHost.Enabled = False : btnJoin.Enabled = False
        peer.ConnectToHost(txtIP.Text.Trim(), port)
    End Sub

    Private Sub Peer_Connected()
        peer.SendLine("HELLO:Client")
        lblStatus.Text = "Da ket noi, dang cho Host bat dau game..."
    End Sub

    Private Sub Peer_Disconnected()
        MessageBox.Show("Mat ket noi voi Host.")
        pnlGame.Visible = False : pnlConnect.Visible = True
        btnHost.Enabled = True : btnJoin.Enabled = True
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If line.StartsWith("WELCOME:") Then
            Integer.TryParse(line.Substring(8), localSeat)
            lblStatus.Text = "Ban la Player " & (localSeat + 1).ToString() & " (" & PlayerNameVN(localSeat) & "). Dang cho Host bat dau..."
        ElseIf line.StartsWith("STATE:") Then
            game.Deserialize(line.Substring(6))
            If Not pnlGame.Visible Then ShowGamePanel()
            RefreshUI()
            AppendLog(game.LastLog)
            CheckAndShowGameOver()
        ElseIf line.StartsWith("CHAT:") Then
            Dim payload As String = line.Substring(5)
            Dim colon As Integer = payload.IndexOf(":"c)
            If colon >= 0 Then
                Dim tag As String = payload.Substring(0, colon)
                Dim msg As String = payload.Substring(colon + 1)
                AppendChat(tag & ": " & msg)
            End If
        End If
    End Sub

    ' ============================================================
    '  HIEN THI CHUNG
    ' ============================================================
    Private Sub ShowGamePanel()
        pnlConnect.Visible = False : pnlGame.Visible = True
        lblYouAre.Text = "Ban la: Player " & (localSeat + 1).ToString() & " (" & PlayerNameVN(localSeat) & ")"
        If lstChat IsNot Nothing Then lstChat.Items.Clear()
        RefreshUI()
    End Sub

    Private Sub RefreshUI()
        If game Is Nothing Then Return

        Dim myTurn As Boolean = (Not game.GameOver) AndAlso (game.CurrentPlayer = localSeat)

        If game.GameOver Then
            lblTurn.Text = "Ket thuc!"
        ElseIf myTurn Then
            lblTurn.Text = "Luot cua BAN (Player " & (localSeat + 1).ToString() & ")"
        Else
            lblTurn.Text = "Luot cua Player " & (game.CurrentPlayer + 1).ToString() & "..."
        End If

        If game.DiceRolled AndAlso Not wasDiceRolledPrev Then
            StartDiceAnim(game.DiceValue)
        ElseIf Not game.DiceRolled AndAlso Not (diceAnimTimer IsNot Nothing AndAlso diceAnimTimer.Enabled) Then
            diceFaceShown = 0
            picDice.Invalidate()
        End If
        wasDiceRolledPrev = game.DiceRolled

        btnRoll.Enabled = myTurn AndAlso Not game.DiceRolled AndAlso Not game.GameOver
        Dim noMove As Boolean = myTurn AndAlso game.DiceRolled AndAlso Not game.HasAnyMove(localSeat, game.DiceValue)
        btnPass.Visible = noMove
        btnRestart.Visible = isHost

        Dim p As Integer
        For p = 0 To 3
            Dim st As String
            If Not game.ActiveSeat(p) Then
                st = "trong"
            ElseIf game.Finished(p) Then
                st = "DA VE DICH!"
            ElseIf p = game.CurrentPlayer AndAlso Not game.GameOver Then
                st = "dang di..."
            Else
                st = "dang choi"
            End If
            lblCardStatus(p).Text = st

            If game.ActiveSeat(p) Then
                Dim homeCount As Integer = 0
                Dim baseCount As Integer = 0
                Dim t As Integer
                For t = 0 To CaNguaGame.TOKENS_PER_PLAYER - 1
                    If game.TokenPos(p, t) = CaNguaGame.POS_FINISH Then homeCount += 1
                    If game.TokenPos(p, t) = CaNguaGame.POS_BASE Then baseCount += 1
                Next t
                lblCardStats(p).Text = "Ve dich: " & homeCount.ToString() & "/4    Trong chuong: " & baseCount.ToString() & "/4"
            Else
                lblCardStats(p).Text = ""
            End If

            pnlPlayers(p).BackColor = If(p = game.CurrentPlayer AndAlso Not game.GameOver, Color.FromArgb(255, 250, 220), Color.White)
        Next p

        boardPanel.Invalidate()
    End Sub

    Private Sub CheckAndShowGameOver()
        If game IsNot Nothing AndAlso game.GameOver Then
            MessageBox.Show(game.LastLog, "Ket thuc!")
        End If
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As EventArgs)
        If Not isHost OrElse game Is Nothing Then Return
        game.ResetGame()
        game.CurrentPlayer = game.FirstActiveSeat()
        AppendLog("Bat dau lai.")
        SyncAfterChange()
    End Sub

    ''' <summary>Log he thong (do xuc xac, di quan, an quan...) duoc gop chung vao khung chat,
    ''' co tien to "⚙" de phan biet voi tin nhan chat cua nguoi choi.</summary>
    Private Sub AppendLog(msg As String)
        AppendChat("⚙ " & msg)
    End Sub

End Class
