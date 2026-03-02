# UI-Feldreihenfolge (Library + Edit)

Diese Reihenfolge wird in der UI konsistent verwendet, damit Filteransicht und Edit-Maske gleich aufgebaut sind.

## 1) Primäre Reihenfolge

1. **Scope**
2. **Main-Module**
3. **Related-Module**
4. **Kundenkürzel**
5. **Tags**
6. **Objektsuche**

## 2) Anwendung in den Views

- **Edit-Maske (`ScriptItemView`)** orientiert sich an derselben fachlichen Reihenfolge: erst Scope, dann Modul-/Kundenkontext, danach weitere Metadaten.
- **Filterbereich (`LibrarySplitView`)** zeigt dieselben Felder in identischer Reihenfolge.

## 3) Erweiterungen

Neue Filter-/Metadatenfelder sollen künftig in diese Reihenfolge einsortiert werden, statt ad hoc in der Oberfläche platziert zu werden.
