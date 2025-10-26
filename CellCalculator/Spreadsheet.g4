grammar Spreadsheet;
/*
 * Parser Rules
 */
compileUnit : expression EOF;

/*
 * Parser Rules
 */

expression
    : expression operatorToken=(ADD | SUBTRACT) expression     #AdditiveExpr
    | expression operatorToken=(MULTIPLY | DIVIDE | MOD | DIV) expression #MultiplicativeExpr
    | unaryOp=(ADD | SUBTRACT) expression                     #UnarySignExpr
    | functionCall                                            #FunctionCallExpr
    | LPAREN expression RPAREN                                #ParenthesizedExpr
    | NUMBER                                                  #NumberExpr
    | CELL_REF                                                #CellReferenceExpr
    | INVALID_REF                                             #InvalidRefExpr
    ;

functionCall : (INC | DEC) LPAREN expression RPAREN;


/*
 * Lexer Rules
 */
CELL_REF : [a-zA-Z]+ [0-9]+;
NUMBER : ('0' | [1-9] [0-9]*);


// Функції та оператори
MOD : 'mod';
DIV : 'div';
INC : 'inc';
DEC : 'dec';

MULTIPLY : '*';
DIVIDE : '/';
SUBTRACT : '-';
ADD : '+';

LPAREN : '(';
RPAREN : ')';

// Ігнорування пробілів
WS : [ \t\r\n] -> channel(HIDDEN);

INVALID_REF : [A-Za-z0-9]+;