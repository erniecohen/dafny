type T = t:T_ | true ghost witness T()
datatype T_ = T()
function test():T { T() as T }
