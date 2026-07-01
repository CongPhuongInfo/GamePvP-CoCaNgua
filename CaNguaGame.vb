Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic

''' <summary>
''' Logic loi choi Co Ca Ngua (Ludo), don gian hoa cho ban 4 nguoi choi online.
''' Duong di la 1 vong 52 o (TRACK), moi nguoi co diem xuat phat rieng cach nhau 13 o:
''' Player 0 -> o 0, Player 1 -> o 13, Player 2 -> o 26, Player 3 -> o 39.
''' Vi tri cua tung quan (Token) duoc luu o dang "khoang cach da di" (localPos) tinh tu
''' diem xuat phat cua chinh nguoi do:
'''   -1        : con nam trong chuong (Base), chua xuat phat.
'''   0..50     : dang di tren duong vong (51 o), o thuc te tren ban = (Start(player)+localPos) Mod 52.
'''   51..56    : dang di trong duong ve nha rieng cua minh (6 o), khong ai an duoc.
'''   57        : da ve dich (Finished).
''' Quy uoc don gian hoa:
'''  - Phai do duoc 6 moi duoc dua quan ra khoi chuong.
'''  - Do duoc 6 thi duoc do tiep luot nua (toi da 3 lan 6 lien tiep, qua 3 lan thi mat luot).
'''  - Quan di vao dung o dang co quan doi phu (khong phai o an toan) -> an quan doi phu ve chuong.
'''  - O an toan (SafeSquares): khong bi an du la quan cua ai.
'''  - Di vuot qua o 57 (dich) la nuoc di khong hop le, phai chon quan khac hoac bo luot.
'''  - Het luot khi: khong do duoc 6 va khong co quan nao di duoc, hoac da danh het 1 luot di.
'''  - Thang: nguoi dau tien dua du 4 quan ve dich.
''' </summary>
Public Class CaNguaGame

    Public Const NUM_PLAYERS As Integer = 4
    Public Const TOKENS_PER_PLAYER As Integer = 4
    Public Const TRACK_LEN As Integer = 52
    Public Const POS_BASE As Integer = -1
    Public Const POS_HOME_START As Integer = 51
    Public Const POS_FINISH As Integer = 57

    Private Shared ReadOnly StartOffset() As Integer = {0, 13, 26, 39}
    Private Shared ReadOnly SafeSquares() As Integer = {0, 8, 13, 21, 26, 34, 39, 47}

    ' TokenPos(player, tokenIndex) = localPos cua quan do
    Public TokenPos(NUM_PLAYERS - 1, TOKENS_PER_PLAYER - 1) As Integer
    Public ActiveSeat(NUM_PLAYERS - 1) As Boolean   ' ghe nao co nguoi choi thuc su
    Public Finished(NUM_PLAYERS - 1) As Boolean     ' nguoi choi da ve het 4 quan

    Public CurrentPlayer As Integer
    Public DiceValue As Integer
    Public DiceRolled As Boolean
    Public ConsecutiveSixes As Integer
    Public GameOver As Boolean
    Public Winner As Integer = -1
    Public LastLog As String

    Private rng As New Random()

    Public Sub New()
        ResetGame()
    End Sub

    Public Sub ResetGame()
        Dim p, t As Integer
        For p = 0 To NUM_PLAYERS - 1
            For t = 0 To TOKENS_PER_PLAYER - 1
                TokenPos(p, t) = POS_BASE
            Next t
            Finished(p) = False
        Next p
        CurrentPlayer = FirstActiveSeat()
        DiceValue = 0
        DiceRolled = False
        ConsecutiveSixes = 0
        GameOver = False
        Winner = -1
        LastLog = "Bat dau game moi."
    End Sub

    Public Function FirstActiveSeat() As Integer
        Dim i As Integer
        For i = 0 To NUM_PLAYERS - 1
            If ActiveSeat(i) Then Return i
        Next i
        Return 0
    End Function

    Public Function GlobalSquare(player As Integer, localPos As Integer) As Integer
        If localPos < 0 OrElse localPos > 50 Then Return -1
        Return (StartOffset(player) + localPos) Mod TRACK_LEN
    End Function

    Public Function IsSafeSquare(globalSq As Integer) As Boolean
        Dim i As Integer
        For i = 0 To SafeSquares.Length - 1
            If SafeSquares(i) = globalSq Then Return True
        Next i
        Return False
    End Function

    ''' <summary>Tra ve True neu quan tokenIdx cua player co the di voi so xuc xac diceVal.</summary>
    Public Function CanMoveToken(player As Integer, tokenIdx As Integer, diceVal As Integer) As Boolean
        Dim pos As Integer = TokenPos(player, tokenIdx)
        If pos = POS_FINISH Then Return False
        If pos = POS_BASE Then Return diceVal = 6
        Return pos + diceVal <= POS_FINISH
    End Function

    Public Function GetMovableTokens(player As Integer, diceVal As Integer) As List(Of Integer)
        Dim result As New List(Of Integer)()
        Dim t As Integer
        For t = 0 To TOKENS_PER_PLAYER - 1
            If CanMoveToken(player, t, diceVal) Then result.Add(t)
        Next t
        Return result
    End Function

    Public Function HasAnyMove(player As Integer, diceVal As Integer) As Boolean
        Return GetMovableTokens(player, diceVal).Count > 0
    End Function

    Public Function RollDice() As Integer
        If GameOver Then Return 0
        DiceValue = rng.Next(1, 7)
        DiceRolled = True
        Return DiceValue
    End Function

    ''' <summary>Ap dat gia tri xuc xac (dung khi Client nhan ket qua tu Host).</summary>
    Public Sub SetDice(value As Integer)
        DiceValue = value
        DiceRolled = True
    End Sub

    ''' <summary>Thuc hien di 1 quan. Tra ve True neu hop le. capturedPlayers chua danh sach
    ''' nguoi choi bi an quan (de bao hieu UI/log).</summary>
    Public Function TryMoveToken(player As Integer, tokenIdx As Integer, ByRef capturedAny As Boolean, ByRef errorMsg As String) As Boolean
        errorMsg = ""
        capturedAny = False

        If GameOver Then
            errorMsg = "Game da ket thuc."
            Return False
        End If
        If player <> CurrentPlayer Then
            errorMsg = "Khong phai luot cua ban."
            Return False
        End If
        If Not DiceRolled Then
            errorMsg = "Ban chua do xuc xac."
            Return False
        End If
        If tokenIdx < 0 OrElse tokenIdx >= TOKENS_PER_PLAYER Then
            errorMsg = "Quan khong hop le."
            Return False
        End If
        If Not CanMoveToken(player, tokenIdx, DiceValue) Then
            errorMsg = "Quan nay khong the di voi so xuc xac hien tai."
            Return False
        End If

        Dim oldPos As Integer = TokenPos(player, tokenIdx)
        Dim newPos As Integer
        If oldPos = POS_BASE Then
            newPos = 0
        Else
            newPos = oldPos + DiceValue
        End If
        TokenPos(player, tokenIdx) = newPos

        Dim capturedNames As New StringBuilder()

        If newPos >= 0 AndAlso newPos <= 50 Then
            Dim globalSq As Integer = GlobalSquare(player, newPos)
            If Not IsSafeSquare(globalSq) Then
                Dim op As Integer, ot As Integer
                For op = 0 To NUM_PLAYERS - 1
                    If op = player OrElse Not ActiveSeat(op) Then Continue For
                    For ot = 0 To TOKENS_PER_PLAYER - 1
                        Dim oPos As Integer = TokenPos(op, ot)
                        If oPos >= 0 AndAlso oPos <= 50 Then
                            If GlobalSquare(op, oPos) = globalSq Then
                                TokenPos(op, ot) = POS_BASE
                                capturedAny = True
                                If capturedNames.Length > 0 Then capturedNames.Append(", ")
                                capturedNames.Append("P" & (op + 1).ToString())
                            End If
                        End If
                    Next ot
                Next op
            End If
        End If

        CheckPlayerFinished(player)
        CheckWin()

        If capturedAny Then
            LastLog = String.Format("Player {0} di quan {1}, an quan cua {2}.", player + 1, tokenIdx + 1, capturedNames.ToString())
        Else
            LastLog = String.Format("Player {0} di quan {1} ({2} buoc).", player + 1, tokenIdx + 1, DiceValue)
        End If

        AdvanceTurnAfterMove(capturedAny, newPos = POS_FINISH)

        Return True
    End Function

    ''' <summary>Goi khi nguoi choi khong co nuoc di hop le (het luot, khong an quan).</summary>
    Public Sub PassTurnNoMove()
        LastLog = String.Format("Player {0} khong co nuoc di, bo luot.", CurrentPlayer + 1)
        ConsecutiveSixes = 0
        MoveToNextActivePlayer()
        DiceRolled = False
        DiceValue = 0
    End Sub

    Private Sub CheckPlayerFinished(player As Integer)
        Dim t As Integer
        Dim allHome As Boolean = True
        For t = 0 To TOKENS_PER_PLAYER - 1
            If TokenPos(player, t) <> POS_FINISH Then
                allHome = False
                Exit For
            End If
        Next t
        If allHome Then Finished(player) = True
    End Sub

    Private Sub CheckWin()
        If GameOver Then Return
        Dim p As Integer
        For p = 0 To NUM_PLAYERS - 1
            If ActiveSeat(p) AndAlso Finished(p) Then
                GameOver = True
                Winner = p
                LastLog = String.Format("Ket thuc! Player {0} ve dich dau tien va thang chung cuoc!", p + 1)
                Return
            End If
        Next p
    End Sub

    Private Sub AdvanceTurnAfterMove(capturedAny As Boolean, justFinishedAToken As Boolean)
        If GameOver Then
            DiceRolled = False
            Return
        End If

        Dim extraTurn As Boolean = False
        If DiceValue = 6 Then
            ConsecutiveSixes += 1
            If ConsecutiveSixes < 3 Then
                extraTurn = True
            Else
                ConsecutiveSixes = 0
            End If
        Else
            ConsecutiveSixes = 0
        End If

        ' An duoc quan hoac vua ve dich cung duoc di tiep 1 luot (quy uoc pho bien).
        If capturedAny OrElse justFinishedAToken Then extraTurn = True

        If Not extraTurn Then
            MoveToNextActivePlayer()
        End If
        DiceRolled = False
        DiceValue = 0
    End Sub

    Public Sub MoveToNextActivePlayer()
        Dim i As Integer
        Dim p As Integer = CurrentPlayer
        For i = 1 To NUM_PLAYERS
            p = (p + 1) Mod NUM_PLAYERS
            If ActiveSeat(p) AndAlso Not Finished(p) Then
                CurrentPlayer = p
                Return
            End If
        Next i
        ' Khong tim duoc nguoi choi tiep theo con choi -> ket thuc.
        GameOver = True
    End Sub

    Public Function ActivePlayerCount() As Integer
        Dim c As Integer = 0
        Dim i As Integer
        For i = 0 To NUM_PLAYERS - 1
            If ActiveSeat(i) Then c += 1
        Next i
        Return c
    End Function

    Public Function Serialize() As String
        Dim sb As New StringBuilder()
        Dim p, t As Integer
        For p = 0 To NUM_PLAYERS - 1
            For t = 0 To TOKENS_PER_PLAYER - 1
                sb.Append(TokenPos(p, t).ToString())
                sb.Append(",")
            Next t
        Next p
        sb.Append("|")
        For p = 0 To NUM_PLAYERS - 1
            sb.Append(If(ActiveSeat(p), "1", "0"))
        Next p
        sb.Append("|")
        For p = 0 To NUM_PLAYERS - 1
            sb.Append(If(Finished(p), "1", "0"))
        Next p
        sb.Append("|")
        sb.Append(CurrentPlayer.ToString()) : sb.Append("|")
        sb.Append(DiceValue.ToString()) : sb.Append("|")
        sb.Append(If(DiceRolled, "1", "0")) : sb.Append("|")
        sb.Append(ConsecutiveSixes.ToString()) : sb.Append("|")
        sb.Append(If(GameOver, "1", "0")) : sb.Append("|")
        sb.Append(Winner.ToString()) : sb.Append("|")
        sb.Append(LastLog.Replace("|", " ").Replace(Chr(13), " ").Replace(Chr(10), " "))
        Return sb.ToString()
    End Function

    Public Sub Deserialize(data As String)
        Dim parts As String() = data.Split("|"c)
        Dim tokenPart As String() = parts(0).Split(","c)
        Dim p, t As Integer
        Dim idx As Integer = 0
        For p = 0 To NUM_PLAYERS - 1
            For t = 0 To TOKENS_PER_PLAYER - 1
                TokenPos(p, t) = Integer.Parse(tokenPart(idx))
                idx += 1
            Next t
        Next p
        Dim seatFlags As String = parts(1)
        For p = 0 To NUM_PLAYERS - 1
            ActiveSeat(p) = (seatFlags(p) = "1"c)
        Next p

        Dim finFlags As String = parts(2)
        For p = 0 To NUM_PLAYERS - 1
            Finished(p) = (finFlags(p) = "1"c)
        Next p

        CurrentPlayer = Integer.Parse(parts(3))
        DiceValue = Integer.Parse(parts(4))
        DiceRolled = (parts(5) = "1")
        ConsecutiveSixes = Integer.Parse(parts(6))
        GameOver = (parts(7) = "1")
        Winner = Integer.Parse(parts(8))
        If parts.Length >= 10 Then LastLog = parts(9)
    End Sub

End Class
