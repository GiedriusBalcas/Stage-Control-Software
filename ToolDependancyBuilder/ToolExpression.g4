grammar ToolExpression;

program: expression* EOF;

expression
    : constant                                              #constantExpression
    | DEVICE_NAME                                           #deviceNameExpression
    | '(' expression ')'                                    #parenthesizedExpression
    | expression '^' (expression | '(' expression ')')      #powerOpExpression
    | expression multOp expression                          #multOpExpression
    | expression addOp expression                           #addOpExpression
    | trigFunc '(' expression ')'                           #trigOpExpression
    | expression expression                                 #doubleExpression
    ;

multOp: '*' | '/' | '%';
addOp: '+' | '-';

constant: INTEGER | FLOAT | STRING ;
trigFunc: 'sin' | 'cos' | 'tan';


INTEGER: '-'?[0-9]+;
FLOAT: '-'?[0-9]+ '.' [0-9]+;
STRING: [a-zA-Z][a-zA-Z][a-zA-Z]*;

// Single-line comment
COMMENT_SINGLE: '//' ~[\r\n]* -> skip;

// Multi-line comment
COMMENT_MULTI: '/*' .*? '*/' -> skip;


// Tokens
DEVICE_NAME:    [a-zA-Z];
WS:             [ \t\r\n]+ -> skip;